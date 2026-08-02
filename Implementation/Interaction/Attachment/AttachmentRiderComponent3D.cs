namespace Jmodot.Implementation.Interaction.Attachment;

using System;
using Godot;
using Jmodot.Core.Actors;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Components;
using Jmodot.Core.Health;
using Jmodot.Core.Movement;
using Jmodot.Core.Interaction;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Shared;

/// <summary>
/// Latches its entity onto an <see cref="IAttachmentHost"/> and rides it. The entity is never
/// reparented: it stays a world-space sibling and writes its own position from the host's live
/// anchor while holding the movement-suspension claim.
///
/// <para>
/// <b>This component owns the whole attach funnel</b> — <see cref="TryAttachTo"/> is the single route
/// onto a host, and <c>ReleaseAttachment</c> the single route off one. No state and no behaviour-tree
/// task holds a claim or mirrors a flag, so there is no bookkeeping anywhere else to get out of step.
/// It also DRIVES the ride: while attached it writes its own position each idle frame, which always
/// runs after the physics frame that moved the host.
/// </para>
///
/// <para>
/// <b>Tuning is data.</b> All five numbers are <see cref="BaseFloatValueDefinition"/>s so a designer
/// picks constant-or-stat-driven per field without a code change. Three resolve through the rider
/// interface; <see cref="FlingForceScale"/> and the re-attach cooldown stay local, since only this
/// rider reads them.
/// </para>
///
/// <para>Required BB key: <see cref="BBDataSig.CharacterController"/> — a rider that cannot write its
/// own position cannot ride at all, so its absence fails initialization rather than producing an
/// entity that latches on and then sits motionless. Optional:
/// <see cref="BBDataSig.MovementProcessor"/> (without it the ride cannot suspend self-movement),
/// <see cref="BBDataSig.KnockbackComponent"/> (without it a shed cannot fling),
/// <see cref="BBDataSig.HurtboxComponent"/> (without it shed damage cannot land),
/// <see cref="BBDataSig.HealthComponent"/>.</para>
/// </summary>
[GlobalClass]
public partial class AttachmentRiderComponent3D : Node3D, IComponent, IBlackboardProvider, IAttachmentRider
{
    /// <summary>How much of a host's capacity budget this rider occupies while attached.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition FootprintDefinition { get; private set; } = null!;

    /// <summary>Force required to shed this rider from a fresh attachment. Refills on each new attach; never regenerates mid-ride.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition MaxGripDefinition { get; private set; } = null!;

    /// <summary>Damage per second dealt to the host while attached.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition AttachDamagePerSecondDefinition { get; private set; } = null!;

    /// <summary>Multiplier converting the force spent shedding this rider into its launch impulse.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition FlingForceScaleDefinition { get; private set; } = null!;

    /// <summary>Seconds after being shed during which this rider refuses to claim a host again. 0 disables the cooldown.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition ReattachCooldownDefinition { get; private set; } = null!;

    private IBlackboard _bb = null!;
    private ICharacterController3D _controller = null!;
    private IMovementProcessor3D? _movement;
    private KnockbackComponent3D? _knockback;
    private HurtboxComponent3D? _hurtbox;
    private IHealth? _health;
    private IStatProvider? _stats;

    private Node? _hostNode;
    private bool _holdsSuspension;
    private ulong _shedAtMsec;

    /// <inheritdoc />
    public (StringName Key, object Value)? Provision => (BBDataSig.AttachmentRider, this);

    /// <inheritdoc />
    public IAttachmentHost? Host { get; private set; }

    /// <inheritdoc />
    public bool IsAttached { get; private set; }

    /// <inheritdoc />
    public float Footprint => this.FootprintDefinition?.ResolveFloatValue(this._stats) ?? 0f;

    /// <inheritdoc />
    public float MaxGrip => this.MaxGripDefinition?.ResolveFloatValue(this._stats) ?? 0f;

    /// <inheritdoc />
    public float AttachDamagePerSecond => this.AttachDamagePerSecondDefinition?.ResolveFloatValue(this._stats) ?? 0f;

    /// <summary>Multiplier converting the force spent shedding this rider into its launch impulse. Read only by this rider.</summary>
    public float FlingForceScale => this.FlingForceScaleDefinition?.ResolveFloatValue(this._stats) ?? 0f;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        ProcessMode = ProcessModeEnum.Disabled;
    }

    /// <summary>
    /// The host cannot always tell the rider it is gone: <c>QueueFree</c> raises no domain event,
    /// and a host freed outside the tree never runs <c>_ExitTree</c>. So the rider verifies every
    /// frame that the host still holds a record for it. Runs from the RESERVATION onward, not just
    /// while riding — a host that dies mid-approach must release the reservation too.
    /// </summary>
    public override void _PhysicsProcess(double delta)
    {
        if (this.Host == null) { return; }
        if (this.HostStillHoldsRecord()) { return; }

        this.ReleaseAttachment();
    }

    /// <summary>
    /// The ride itself. Idle-frame rather than physics-frame on purpose: <c>_Process</c> always runs
    /// after the physics frame that moved the host, so the rider reads a settled anchor without any
    /// cross-entity execution-order contract for a designer to get wrong.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!this.IsAttached) { return; }
        if (!this.TryGetRideWorldPosition(out var anchor)) { return; }

        this._controller.Teleport(anchor);
    }

    /// <summary>
    /// Re-arms the death hook that <see cref="_ExitTree"/> drops. Reparenting fires both, so without
    /// this the rider survives the move with no way to notice its own death.
    /// </summary>
    public override void _EnterTree()
    {
        if (this._health == null) { return; }

        this._health.OnDied -= this.OnOwnDeath;
        this._health.OnDied += this.OnOwnDeath;
    }

    public override void _ExitTree()
    {
        if (this._health != null) { this._health.OnDied -= this.OnOwnDeath; }
        if (this.Host == null) { return; }

        var host = this.Host;
        var hostNode = this._hostNode;
        this.ReleaseAttachment();
        if (GodotObject.IsInstanceValid(hostNode)) { host.Detach(this, DetachCause.RiderRemoved); }
    }

    /// <inheritdoc />
    public void OnReserved(IAttachmentHost host, Vector3 localAnchor)
    {
        this.Host = host;
        this._hostNode = host.GetUnderlyingNode();
    }

    /// <inheritdoc />
    public void OnAttached(IAttachmentHost host, Vector3 localAnchor)
    {
        this.Host = host;
        this._hostNode = host.GetUnderlyingNode();
        this.IsAttached = true;
    }

    /// <inheritdoc />
    public void OnShed(Vector3 direction, float spentForce, Node? attributedSource)
    {
        // Only a shed arms the cooldown. A deliberate detach — death, an aborted approach, the owner
        // letting go — is not the entity being thrown off, so it must not be punished with a wait.
        this._shedAtMsec = Time.GetTicksMsec();

        // Ordering is load-bearing: a suspended processor CLEARS its pending impulses every tick,
        // so an impulse applied before the release is discarded rather than queued.
        this.ReleaseAttachment();

        var impulse = spentForce * this.FlingForceScale;
        if (impulse <= 0f) { return; }
        if (this._knockback == null) { return; }

        this._knockback.ApplyKnockback(direction, impulse, attributedSource);
    }

    /// <inheritdoc />
    public void OnDetached(DetachCause cause)
    {
        this.ReleaseAttachment();
    }

    /// <inheritdoc />
    public bool TryApplyShedDamage(IAttackPayload payload)
    {
        if (payload == null) { return false; }
        if (this._hurtbox == null || !GodotObject.IsInstanceValid(this._hurtbox)) { return false; }

        return this._hurtbox.ProcessHit(payload);
    }

    /// <summary>
    /// The single route onto a host: cooldown peek, reservation, then the suspension claim.
    /// </summary>
    /// <remarks>
    /// The order is load-bearing and cheap-and-non-mutating first. The suspension claim is taken with
    /// <see cref="SuspensionVelocityPolicy.Zero"/>, so taking it speculatively would brake a chasing
    /// entity on every refusal — and with several riders contending for one host, capacity refusal is the
    /// COMMON case, not the exceptional one.
    /// </remarks>
    /// <returns>False when any step refused; the rider is left holding nothing either way.</returns>
    public bool TryAttachTo(IAttachmentHost host)
    {
        if (host == null) { return false; }
        if (this.IsReattachOnCooldown) { return false; }
        if (!host.TryReserve(this, out _)) { return false; }
        if (this.TryClaimPositionalAuthority()) { return true; }

        host.Detach(this, DetachCause.RiderAborted);
        this.ReleaseAttachment();
        return false;
    }

    /// <summary>
    /// Take exclusive positional authority over this entity, suspending its own movement pump.
    /// Idempotent for this component, which is the sole claimant for the whole ride.
    /// </summary>
    public bool TryClaimPositionalAuthority()
    {
        // Enforced here rather than at the caller: every route onto a host passes through this claim, so
        // a refusal reads to the BT as an ordinary failed attach and the task retries on its own cadence.
        if (this.IsReattachOnCooldown) { return false; }

        if (this._movement == null)
        {
            JmoLogger.Warning(this, "[Attachment] No IMovementProcessor3D on the blackboard — this rider cannot ride.");
            return false;
        }

        // Zero, not Preserve: the claim is taken mid-chase, and the release happens on a shed. Resuming
        // a seconds-old chase vector pointed AT the host can cancel the fling that just threw the rider off.
        if (!this._movement.TryClaimSuspension(Name, SuspensionVelocityPolicy.Zero)) { return false; }

        this._holdsSuspension = true;
        return true;
    }

    /// <summary>Give positional authority back. Safe to call when the claim is not held.</summary>
    public void ReleasePositionalAuthority()
    {
        if (!this._holdsSuspension) { return; }

        this._holdsSuspension = false;
        this._movement?.ReleaseSuspension(Name);
    }

    /// <summary>
    /// Where this rider should sit this frame: the host's live anchor, verbatim. Any off-plane depth
    /// is already baked into the anchor by the host's <c>AttachmentAnchorProfile3D</c> — how riders
    /// are arranged across a silhouette is the host's decision, not each rider's.
    /// </summary>
    public bool TryGetRideWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.Zero;
        // Answers from the RESERVATION onward: the anchor is precisely what the approach flies toward,
        // so gating this on IsAttached would leave the rider with no destination.
        if (this.Host == null) { return false; }

        return this.Host.TryGetAnchorWorldPosition(this, out worldPosition);
    }

    /// <summary>Clears BOTH phases' state — a reservation abandoned mid-flight unwinds through here too.</summary>
    private void ReleaseAttachment()
    {
        if (this.Host == null && !this.IsAttached) { return; }

        this.IsAttached = false;
        this.Host = null;
        this._hostNode = null;
        this.ReleasePositionalAuthority();
    }

    /// <summary>
    /// True while a shed still bars this rider from claiming a host. Readable from outside because the
    /// attach funnel peeks at it FIRST — the peek is side-effect-free, where every step behind it is not.
    /// </summary>
    public bool IsReattachOnCooldown
    {
        get
        {
            if (this._shedAtMsec == 0uL) { return false; }

            var cooldown = this.ReattachCooldownDefinition?.ResolveFloatValue(this._stats) ?? 0f;
            if (cooldown <= 0f) { return false; }

            var elapsed = (Time.GetTicksMsec() - this._shedAtMsec) / 1000f;
            return elapsed < cooldown;
        }
    }

    private bool HostStillHoldsRecord()
    {
        if (this.Host == null) { return false; }
        if (!GodotObject.IsInstanceValid(this._hostNode)) { return false; }

        // Ancestor walk, not a single node: QueueFree stamps the flag on the node it was called on
        // only, so a host component whose ENTITY is being freed reports false for its own queued state.
        for (Node? ancestor = this._hostNode; ancestor != null; ancestor = ancestor.GetParent())
        {
            if (ancestor.IsQueuedForDeletion()) { return false; }
        }

        foreach (var record in this.Host.Attachments)
        {
            if (ReferenceEquals(record.Rider, this)) { return true; }
        }

        return false;
    }

    private void OnOwnDeath(HealthChangeEventArgs args)
    {
        if (this.Host == null) { return; }

        var host = this.Host;
        this.ReleaseAttachment();
        host.Detach(this, DetachCause.RiderDied);
    }

    #region IComponent

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    public bool Initialize(IBlackboard bb)
    {
        this._bb = bb;
        // Pool reuse re-runs Initialize on a component whose previous life ended in a shed; a recycled
        // instance must not inherit the last entity's cooldown.
        this._shedAtMsec = 0uL;
        bb.TryGet<IStatProvider>(BBDataSig.Stats, out this._stats);

        // HARD dependency, unlike every other resolve here: the rider WRITES its own position for the
        // whole ride, so without a controller it would latch onto a host and then sit still. Failing
        // initialization makes the entity initializer retract this component's blackboard provision, so
        // consumers fail loudly instead of holding a rider that cannot ride.
        if (!bb.TryGet<ICharacterController3D>(BBDataSig.CharacterController, out var controller) || controller == null)
        {
            JmoLogger.Debug(this, "[Attachment] No ICharacterController3D on the blackboard — this rider cannot ride.");
            return false;
        }

        this._controller = controller;
        bb.TryGet<IMovementProcessor3D>(BBDataSig.MovementProcessor, out this._movement);
        bb.TryGet<KnockbackComponent3D>(BBDataSig.KnockbackComponent, out this._knockback);
        bb.TryGet<HurtboxComponent3D>(BBDataSig.HurtboxComponent, out this._hurtbox);
        bb.TryGet<IHealth>(BBDataSig.HealthComponent, out this._health);

        IsInitialized = true;
        Initialized();
        return true;
    }

    public void OnPostInitialize()
    {
        ProcessMode = ProcessModeEnum.Inherit;

        if (this._knockback == null)
        {
            JmoLogger.Warning(this, "[Attachment] No KnockbackComponent3D on the blackboard — sheds will not fling this rider.");
        }

        if (this._health == null) { return; }

        // Idempotent: OnPostInitialize re-runs on pool reuse and scene rebind.
        this._health.OnDied -= this.OnOwnDeath;
        this._health.OnDied += this.OnOwnDeath;
    }

    public Node GetUnderlyingNode() => this;

    #endregion

    #region Test Helpers
#if TOOLS

    internal void SetTuning(
        BaseFloatValueDefinition footprint,
        BaseFloatValueDefinition maxGrip,
        BaseFloatValueDefinition attachDps,
        BaseFloatValueDefinition flingForceScale)
    {
        this.FootprintDefinition = footprint;
        this.MaxGripDefinition = maxGrip;
        this.AttachDamagePerSecondDefinition = attachDps;
        this.FlingForceScaleDefinition = flingForceScale;
    }

    internal void SetReattachCooldownSeconds(float seconds)
        => this.ReattachCooldownDefinition = new ConstantFloatDefinition(seconds);

    internal bool _TestHoldsSuspension => this._holdsSuspension;

#endif
    #endregion
}
