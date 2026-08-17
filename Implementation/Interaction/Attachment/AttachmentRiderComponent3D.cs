namespace Jmodot.Implementation.Interaction.Attachment;

using System;
using System.Collections.Generic;
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
using Jmodot.Core.Visual.Animation.Sprite;
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
/// <b>Tuning is data.</b> All seven numbers are <see cref="BaseFloatValueDefinition"/>s so a designer
/// picks constant-or-stat-driven per field without a code change. Three resolve through the rider
/// interface; <see cref="FlingForceScale"/>, the re-attach cooldown and the attack tick interval stay
/// local, since only this rider reads them.
/// </para>
///
/// <para>Required BB key: <see cref="BBDataSig.CharacterController"/> — a rider that cannot write its
/// own position cannot ride at all, so its absence fails initialization rather than producing an
/// entity that latches on and then sits motionless. Optional:
/// <see cref="BBDataSig.MovementProcessor"/> (without it the ride cannot suspend self-movement),
/// <see cref="BBDataSig.KnockbackComponent"/> (without it a shed cannot fling),
/// <see cref="BBDataSig.HurtboxComponent"/> (without it shed damage cannot land),
/// <see cref="BBDataSig.HealthComponent"/>,
/// <see cref="BBDataSig.AnimationOrchestrator"/> (without it the pose clips cannot be checked for
/// existence at load).</para>
/// </summary>
[GlobalClass, Tool]
public partial class AttachmentRiderComponent3D : Node3D, IComponent, IBlackboardProvider, IAttachmentRider
{
    /// <summary>How much of a host's capacity budget this rider occupies while attached.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition FootprintDefinition { get; private set; } = null!;

    /// <summary>Force required to shed this rider from a fresh attachment. Refills on each new attach; never regenerates mid-ride.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition MaxGripDefinition { get; private set; } = null!;

    /// <summary>Damage per second dealt to the host while attached.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition AttachDamagePerSecondDefinition { get; private set; } = null!;

    /// <summary>
    /// Damage dealt to the host by ONE physical contact that failed to become an attachment — the
    /// missed-jump hit. Separate from the ride drain because they answer different questions: what a
    /// single impact costs versus what a second of riding costs.
    /// </summary>
    [Export, RequiredExport] public BaseFloatValueDefinition ContactDamageDefinition { get; private set; } = null!;

    /// <summary>Multiplier converting the force spent shedding this rider into its launch impulse.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition FlingForceScaleDefinition { get; private set; } = null!;

    /// <summary>Seconds after being shed during which this rider refuses to claim a host again. 0 disables the cooldown.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition ReattachCooldownDefinition { get; private set; } = null!;

    /// <summary>
    /// Degrees to tilt a shed fling up from the blow's own direction. 0 keeps the flat launch. Any
    /// value above 0 also marks the impulse as deliberately vertical, so the receiving knockback
    /// component's flatten safety net leaves the arc intact instead of zeroing it.
    /// </summary>
    [Export] public BaseFloatValueDefinition? FlingUpwardAngleDefinition { get; private set; }

    /// <summary>
    /// Random spread in degrees applied either side of the fling's upward angle, so several riders
    /// shed by one blow scatter instead of leaving on a single repeated arc. 0 makes every fling
    /// identical.
    /// </summary>
    [Export] public BaseFloatValueDefinition? FlingUpwardAngleJitterDefinition { get; private set; }

    /// <summary>
    /// The attach visuals this rider's art provides. Unset leaves the pose-less behaviour: no pose is
    /// booked and the ride position stays the host's placed anchor.
    /// </summary>
    [Export] public AttachPoseSet? AttachPoses { get; private set; }

    /// <summary>
    /// The single pose a rider falls back to when no roster is authored and it holds none — the
    /// pose-less rider's one pose. The only surface reachable when <see cref="AttachPoses"/> is null,
    /// so the global fallback lives here rather than on the set.
    /// </summary>
    [Export] public AttachPose? DefaultPose { get; private set; }

    /// <summary>Seconds between attack damage ticks while attached. The scheduler owns the cadence; the
    /// per-tick amount is the rider's damage-per-second scaled by this.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition AttackTickIntervalDefinition { get; private set; } = null!;

    private IBlackboard _bb = null!;
    private ICharacterController3D _controller = null!;
    private IMovementProcessor3D? _movement;
    private KnockbackComponent3D? _knockback;
    private HurtboxComponent3D? _hurtbox;
    private IHealth? _health;
    private IStatProvider? _stats;
    private IAnimationOrchestrator? _orchestrator;

    private Node? _hostNode;
    private bool _holdsSuspension;
    private ulong _shedAtMsec;
    private JmoRng? _flingRng;

    private CollisionObject3D? _body;
    private bool _bodyCollisionSuspended;
    private uint _savedCollisionLayer;
    private uint _savedCollisionMask;

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

    /// <summary>Degrees a shed fling tilts up from the blow's direction, before jitter. Read only by this rider.</summary>
    public float FlingUpwardAngle => this.FlingUpwardAngleDefinition?.ResolveFloatValue(this._stats) ?? 0f;

    /// <summary>Random spread either side of <see cref="FlingUpwardAngle"/>, in degrees. Read only by this rider.</summary>
    public float FlingUpwardAngleJitter => this.FlingUpwardAngleJitterDefinition?.ResolveFloatValue(this._stats) ?? 0f;

    /// <summary>
    /// Damage one failed-attach contact deals to the host. Stat-resolvable like every other attachment
    /// number, so a buffed roach hits harder on a bounce without a second authored surface.
    /// </summary>
    public float ContactDamage => this.ContactDamageDefinition?.ResolveFloatValue(this._stats) ?? 0f;

    /// <summary>
    /// The pose this rider currently holds, or null while it holds none. DERIVED from the host's record
    /// — the single home for the assignment — rather than mirrored here: a stored copy would need
    /// clearing on every one of the release paths, and the one that got missed would leave a released
    /// rider still rendering a pose it no longer holds.
    /// </summary>
    public AttachPose? AssignedPose
    {
        get
        {
            if (this.Host == null) { return null; }

            foreach (var record in this.Host.Attachments)
            {
                if (ReferenceEquals(record.Rider, this)) { return record.Pose; }
            }

            return null;
        }
    }

    /// <summary>The single pose in effect right now: the assigned one when holding, else the authored
    /// default. Assigned wins because it reflects what is actually on screen.</summary>
    public AttachPose? ActivePose => this.AssignedPose ?? this.DefaultPose;

    /// <summary>The ride clip the current pose plays, or empty when the rider holds no pose.</summary>
    public StringName ActiveRideClip => this.ActivePose?.RideAnimationName ?? new StringName();

    /// <summary>The attack clip a landed tick claims, or empty when the rider holds no pose.</summary>
    public StringName ActiveAttackClip => this.ActivePose?.AttackAnimationName ?? new StringName();

    /// <summary>How long one attack tick's claim survives for the current pose; 0 falls back to the tick
    /// interval at the scheduler.</summary>
    public float ActiveAttackHoldSeconds => this.ActivePose?.AttackAnimationHoldSeconds ?? 0f;

    /// <summary>Seconds between attack damage ticks, resolved from the authored cadence definition.</summary>
    public float AttackTickInterval => this.AttackTickIntervalDefinition.ResolveFloatValue(this._stats);

    /// <summary>
    /// The attachment became real (the host confirmed the arrival). For consumers whose behaviour is
    /// scoped to the ride itself — pose overlays, host-facing mirroring — rather than to the approach.
    /// </summary>
    public event Action<IAttachmentHost> AttachmentStarted = delegate { };

    /// <summary>
    /// The attachment ended, however it ended. Raised from <c>ReleaseAttachment</c>, the single point
    /// every exit path funnels through — shed, host-vanished poll, tree exit, death, plain detach — so a
    /// subscriber cannot be left holding ride-scoped state. <c>OnDetached</c> would NOT do: the shed and
    /// host-vanished paths never reach it.
    /// </summary>
    public event Action<DetachCause> AttachmentEnded = delegate { };

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

        this.ReleaseAttachment(DetachCause.HostRemoved);
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
        this.ReleaseAttachment(DetachCause.RiderRemoved);
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
        this.AttachmentStarted.Invoke(host);
    }

    /// <inheritdoc />
    public void OnShed(Vector3 direction, float spentForce, float attackKnockbackForce, Node? attributedSource)
    {
        // Only a shed arms the cooldown. A deliberate detach — death, an aborted approach, the owner
        // letting go — is not the entity being thrown off, so it must not be punished with a wait.
        this._shedAtMsec = Time.GetTicksMsec();

        // Ordering is load-bearing: a suspended processor CLEARS its pending impulses every tick,
        // so an impulse applied before the release is discarded rather than queued.
        this.ReleaseAttachment(DetachCause.Shed);

        // The fling scales the ATTACK's knockback when the attacker provides one — the blow the
        // player threw is what throws the rider, and grip only decides WHO comes off. The spent
        // force stays the fallback so hosts that shake riders off without an authored knockback
        // keep their behaviour.
        var flingBase = attackKnockbackForce > 0f ? attackKnockbackForce : spentForce;
        var impulse = flingBase * this.FlingForceScale;
        if (impulse <= 0f) { return; }
        if (this._knockback == null) { return; }

        var (flingDirection, preserveVertical) = this.ResolveFlingArc(direction);
        this._knockback.ApplyKnockback(flingDirection, impulse, attributedSource, preserveVertical);
    }

    /// <summary>
    /// Tilts a shed's direction up by the authored arc plus its jitter.
    /// </summary>
    /// <returns>
    /// The launch direction, and whether it carries a vertical the receiver must not flatten. Both
    /// halves are load-bearing together: the receiving knockback component zeroes Y by default, so a
    /// tilted direction sent without the flag is silently discarded one step before it is used.
    /// </returns>
    /// <remarks>
    /// A jittered angle that lands at or below zero returns the flat launch rather than aiming the
    /// rider into the floor, which makes jitter safe to author wider than the base arc.
    /// </remarks>
    private (Vector3 Direction, bool PreserveVertical) ResolveFlingArc(Vector3 direction)
    {
        var degrees = this.FlingUpwardAngle;
        var jitter = this.FlingUpwardAngleJitter;
        if (jitter > 0f)
        {
            this._flingRng ??= JmoRng.NonDeterministic();
            degrees += this._flingRng.GetRndInRange(-jitter, jitter);
        }

        if (degrees <= 0f) { return (direction, false); }

        var rad = Mathf.DegToRad(degrees);
        return ((direction * Mathf.Cos(rad) + Vector3.Up * Mathf.Sin(rad)).Normalized(), true);
    }

    /// <inheritdoc />
    public void OnDetached(DetachCause cause)
    {
        this.ReleaseAttachment(cause);
    }

    /// <inheritdoc />
    public bool TryApplyShedDamage(IAttackPayload payload, Vector3? impactDirection = null)
    {
        if (payload == null) { return false; }
        if (this._hurtbox == null || !GodotObject.IsInstanceValid(this._hurtbox)) { return false; }

        return this._hurtbox.ProcessHit(payload, impactDirection);
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
        this.ReleaseAttachment(DetachCause.RiderAborted);
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
        this.SuspendBodyCollision();
        return true;
    }

    /// <summary>Give positional authority back. Safe to call when the claim is not held.</summary>
    public void ReleasePositionalAuthority()
    {
        if (!this._holdsSuspension) { return; }

        this._holdsSuspension = false;
        this.RestoreBodyCollision();
        this._movement?.ReleaseSuspension(Name);
    }

    /// <summary>
    /// While authority is held the body is teleported through the host's collider every frame; a live
    /// layer/mask there is a depenetration ramp the host's own move-and-slide climbs, launching the
    /// host skyward with no force ever logged. Area children (hurtbox, sensors) keep their own layers,
    /// so a riding entity can still be hit. Scoped to the authority claim, not the attachment: the
    /// claim starts before the approach flight, which already overlaps the host.
    /// </summary>
    private void SuspendBodyCollision()
    {
        if (this._bodyCollisionSuspended) { return; }
        if (this._body == null || !GodotObject.IsInstanceValid(this._body)) { return; }

        this._savedCollisionLayer = this._body.CollisionLayer;
        this._savedCollisionMask = this._body.CollisionMask;
        this._body.CollisionLayer = 0u;
        this._body.CollisionMask = 0u;
        this._bodyCollisionSuspended = true;
    }

    private void RestoreBodyCollision()
    {
        if (!this._bodyCollisionSuspended) { return; }

        this._bodyCollisionSuspended = false;
        if (this._body == null || !GodotObject.IsInstanceValid(this._body)) { return; }

        this._body.CollisionLayer = this._savedCollisionLayer;
        this._body.CollisionMask = this._savedCollisionMask;
    }

    /// <summary>
    /// Where this rider should sit this frame: the host's live anchor, verbatim. Any off-plane depth
    /// is already baked into the anchor by the host's <see cref="AttachmentAnchorProfile3D"/> — how riders
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

    /// <summary>
    /// Clears BOTH phases' state — a reservation abandoned mid-flight unwinds through here too — and
    /// announces the end. The single release point, which is why <see cref="AttachmentEnded"/> is raised
    /// here and nowhere else: the shed, the host-vanished poll and the tree exit all bypass
    /// <see cref="OnDetached"/>.
    /// </summary>
    private void ReleaseAttachment(DetachCause cause)
    {
        if (this.Host == null && !this.IsAttached) { return; }

        this.IsAttached = false;
        this.Host = null;
        this._hostNode = null;
        this.ReleasePositionalAuthority();
        this.AttachmentEnded.Invoke(cause);
    }

    /// <inheritdoc />
    public float SecondsSinceShed
        => this._shedAtMsec == 0uL
            ? float.PositiveInfinity
            : (Time.GetTicksMsec() - this._shedAtMsec) / 1000f;

    /// <inheritdoc />
    public bool IsReattachOnCooldown
    {
        get
        {
            var cooldown = this.ReattachCooldownDefinition?.ResolveFloatValue(this._stats) ?? 0f;
            return cooldown > 0f && this.SecondsSinceShed < cooldown;
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

    /// <summary>
    /// Pose ids and the entity's animation library are two hand-synced surfaces: a renamed or unauthored
    /// clip costs nothing at load and shows nothing at play. One Error naming EVERY missing clip is the
    /// drift guard — a per-use warning would emit one indistinguishable line per frame of riding and bury
    /// the authoring mistake it is reporting.
    /// </summary>
    private void ReportMissingPoseClips()
    {
        if (this._orchestrator == null) { return; }

        var missing = new List<string>();

        void Check(AttachPose pose)
        {
            if (!this._orchestrator.HasAnimationBase(pose.RideAnimationName)) { missing.Add(pose.RideAnimationName.ToString()); }
            if (!this._orchestrator.HasAnimationBase(pose.AttackAnimationName)) { missing.Add(pose.AttackAnimationName.ToString()); }
        }

        if (this.AttachPoses != null)
        {
            foreach (var pose in this.AttachPoses.ValidatedPoses) { Check(pose); }
        }

        if (this.DefaultPose != null) { Check(this.DefaultPose); }

        if (missing.Count == 0) { return; }

        JmoLogger.Error(this,
            $"[Attachment] {missing.Count} authored pose clip(s) do not exist on this entity's animator: "
            + $"{string.Join(", ", missing)}. Those poses will render nothing while held.");
    }

    private void OnOwnDeath(HealthChangeEventArgs args)
    {
        if (this.Host == null) { return; }

        var host = this.Host;
        this.ReleaseAttachment(DetachCause.RiderDied);
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
        // Authored-pose contract, enforced here so a DefaultPose that can never render fails at load
        // rather than after a rider latches onto a host.
        this.DefaultPose?.Validate();
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
        this._body = controller.GetUnderlyingNode() as CollisionObject3D;
        bb.TryGet<IMovementProcessor3D>(BBDataSig.MovementProcessor, out this._movement);
        bb.TryGet<KnockbackComponent3D>(BBDataSig.KnockbackComponent, out this._knockback);
        bb.TryGet<HurtboxComponent3D>(BBDataSig.HurtboxComponent, out this._hurtbox);
        bb.TryGet<IHealth>(BBDataSig.HealthComponent, out this._health);
        bb.TryGet<IAnimationOrchestrator>(BBDataSig.AnimationOrchestrator, out this._orchestrator);

        IsInitialized = true;
        Initialized();
        return true;
    }

    public void OnPostInitialize()
    {
        ProcessMode = ProcessModeEnum.Inherit;

        this.ReportMissingPoseClips();

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
        // Filled so a rig that never authors a contact hit still satisfies the required export; suites
        // that exercise the missed-jump hit call SetContactDamage explicitly.
        this.ContactDamageDefinition = new ConstantFloatDefinition(0f);
        // Filled so a rig that never authors a cadence still satisfies the required export; suites that
        // exercise the tick call SetAttackTickInterval explicitly.
        this.AttackTickIntervalDefinition = new ConstantFloatDefinition(0.25f);
    }

    internal void SetReattachCooldownSeconds(float seconds)
        => this.ReattachCooldownDefinition = new ConstantFloatDefinition(seconds);

    internal void SetFlingUpwardAngle(float degrees, float jitterDegrees = 0f)
    {
        this.FlingUpwardAngleDefinition = new ConstantFloatDefinition(degrees);
        this.FlingUpwardAngleJitterDefinition = new ConstantFloatDefinition(jitterDegrees);
    }

    internal (Vector3 Direction, bool PreserveVertical) _TestResolveFlingArc(Vector3 direction)
        => this.ResolveFlingArc(direction);

    internal void SetContactDamage(float amount)
        => this.ContactDamageDefinition = new ConstantFloatDefinition(amount);

    internal void SetAttackTickInterval(float seconds)
        => this.AttackTickIntervalDefinition = new ConstantFloatDefinition(seconds);

    internal void SetAttachPoses(AttachPoseSet? poses) => this.AttachPoses = poses;

    internal void SetDefaultPose(AttachPose? pose) => this.DefaultPose = pose;

    internal bool _TestHoldsSuspension => this._holdsSuspension;

#endif
    #endregion
}
