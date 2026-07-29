namespace Jmodot.Implementation.Interaction.Attachment;

using System;
using Godot;
using Jmodot.Core.Actors;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Components;
using Jmodot.Core.Health;
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
/// <b>This component owns the suspension claim identity</b> for the whole ride. The states that
/// drive the approach and the ride claim through <see cref="TryClaimPositionalAuthority"/> rather
/// than each claiming under their own name, so the handoff from approach to ride is an idempotent
/// same-owner re-claim with no window where the entity is briefly self-driving.
/// </para>
///
/// <para>
/// <b>Tuning is data.</b> All four numbers are <see cref="BaseFloatValueDefinition"/>s so a designer
/// picks constant-or-stat-driven per field without a code change. Three resolve through the rider
/// interface; <see cref="FlingForceScale"/> stays local, since only this rider ever converts spent
/// force into its own impulse.
/// </para>
///
/// <para>Optional BB keys: <see cref="BBDataSig.MovementProcessor"/> (without it the ride cannot
/// suspend self-movement), <see cref="BBDataSig.KnockbackComponent"/> (without it a shed cannot
/// fling), <see cref="BBDataSig.HurtboxComponent"/> (without it shed damage cannot land),
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

    private IBlackboard _bb = null!;
    private IMovementProcessor3D? _movement;
    private KnockbackComponent3D? _knockback;
    private HurtboxComponent3D? _hurtbox;
    private IHealth? _health;
    private IStatProvider? _stats;

    private Node? _hostNode;
    private bool _holdsSuspension;

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
        this._bb?.Set(BBDataSig.IsAttached, true);
    }

    /// <inheritdoc />
    public void OnShed(Vector3 direction, float spentForce, Node? attributedSource)
    {
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
    /// Take exclusive positional authority over this entity, suspending its own movement pump.
    /// Idempotent for this component, which is the sole claimant for the whole ride.
    /// </summary>
    public bool TryClaimPositionalAuthority()
    {
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
        this._bb?.Set(BBDataSig.IsAttached, false);
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
        bb.TryGet<IStatProvider>(BBDataSig.Stats, out this._stats);
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
        this._bb?.Set(BBDataSig.IsAttached, this.IsAttached);

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

    internal bool _TestHoldsSuspension => this._holdsSuspension;

#endif
    #endregion
}
