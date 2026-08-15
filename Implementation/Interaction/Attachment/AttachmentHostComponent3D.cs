namespace Jmodot.Implementation.Interaction.Attachment;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.Interaction;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Visual;

/// <summary>
/// Carries <see cref="IAttachmentRider"/>s against a capacity budget and shakes them off when a
/// forceful action spends enough force against their grip.
///
/// <para>
/// This component owns the LIVE state — the roster, each attachment's remaining grip, and the
/// footprint already consumed. The capacity RULE is config (a shared
/// <see cref="AttachmentCapacityProvider3D"/> Resource) and the host's stats are config too;
/// neither may hold per-attachment state, since both are shared across every host that authors them.
/// </para>
///
/// <para>
/// <b>A host never holds a record for a freed rider.</b> Three independent releases guarantee it,
/// because no single one covers every way a rider can vanish: the rider's own
/// <c>TreeExiting</c> (subscribed here), the rider's death (the rider detaches itself), and a
/// per-physics-frame sweep of the roster. The sweep checks queued-for-deletion as well as instance
/// validity — <c>IsInstanceValid</c> still answers true for a node that has been
/// <c>QueueFree</c>d and will be gone next frame.
/// </para>
///
/// <para>Required BB key: none. Optional: <see cref="BBDataSig.Agent"/> (the entity whose art is
/// measured and whose transform anchors are expressed in — falls back to this node),
/// <see cref="BBDataSig.Stats"/>, <see cref="BBDataSig.EntitySeed"/>.</para>
/// </summary>
[GlobalClass]
public partial class AttachmentHostComponent3D : Node3D, IComponent, IBlackboardProvider, IAttachmentHost
{
    /// <summary>Rule deciding how much rider footprint this host carries at once.</summary>
    [Export, RequiredExport] public AttachmentCapacityProvider3D CapacityProvider { get; private set; } = null!;

    /// <summary>Rule deciding where on this host's silhouette each rider is seated.</summary>
    [Export, RequiredExport] public AttachmentAnchorProfile3D AnchorProfile { get; private set; } = null!;

    /// <summary>
    /// Seconds between capacity sweeps. A bounds-derived capacity re-measures the host's whole art
    /// subtree, which is far too expensive to repeat every physics frame for a rule whose answer
    /// changes on the timescale of animation; a quarter-second eviction latency is imperceptible.
    /// </summary>
    private const float CapacitySweepInterval = 0.25f;

    private readonly List<AttachmentRecord> _records = new();
    private readonly Dictionary<IAttachmentRider, Action> _treeExitHandlers = new();

    private IStatProvider? _stats;
    private Node3D _entity = null!;
    private JmoRng _rng = null!;
    private bool _warnedMissingSeed;
    private bool _warnedUnmeasuredBounds;
    private int _nextAttachSequence;
    private float _sinceCapacitySweep;

    /// <inheritdoc />
    public (StringName Key, object Value)? Provision => (BBDataSig.AttachmentHost, this);

    /// <inheritdoc />
    public float Capacity => this.CapacityProvider?.GetCapacity(this.MeasureBounds(), this._stats, this._entity) ?? 0f;

    /// <inheritdoc />
    public float UsedFootprint
    {
        get
        {
            var used = 0f;
            foreach (var record in this._records) { used += record.Footprint; }
            return used;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AttachmentRecord> Attachments => this._records;

    /// <inheritdoc />
    public Node3D HostEntity => GodotObject.IsInstanceValid(this._entity) ? this._entity : this;

    /// <inheritdoc />
    public event Action<IAttachmentRider, DetachCause> RiderDetached = delegate { };

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        ProcessMode = ProcessModeEnum.Disabled;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Liveness stays per-frame — it is a handful of validity checks, and a record for a freed
        // rider must never survive a frame. Only the capacity sweep is throttled.
        this.ReleaseDeadRecords();

        this._sinceCapacitySweep += (float)delta;
        if (this._sinceCapacitySweep < CapacitySweepInterval) { return; }

        this._sinceCapacitySweep = 0f;
        this.EnforceCapacity();
    }

    /// <summary>
    /// The host is leaving: every rider is released here, because <c>QueueFree</c> raises no domain
    /// event a rider could subscribe to. Riders additionally poll this host's validity, which is
    /// what covers a host freed without this ever running.
    /// </summary>
    public override void _ExitTree()
    {
#if TOOLS
        if (this._TestSuppressExitTreeDetach) { return; }
#endif
        this.DetachAll(DetachCause.HostRemoved);
    }

    /// <inheritdoc />
    public bool TryReserve(IAttachmentRider rider, out AttachmentRecord record)
    {
        record = default;
        if (rider == null) { return false; }

        // Already booked HERE: idempotent, and it hands back the LIVE record rather than a default one —
        // a caller re-entering the attach path must read its real anchor, not the origin. Refusing
        // instead reads to that caller as a failed attach and tears down a perfectly live attachment.
        // Ahead of every feasibility gate on purpose: this is a hand-back, not a seating decision.
        var booked = this.IndexOf(rider);
        if (booked >= 0)
        {
            record = this._records[booked];
            return true;
        }

        if (!this.CanSeat(rider)) { return false; }

        // Pose availability sits HERE rather than in CanSeat, so the public peek stays capacity-only: a
        // committing caller is expected to absorb this refusal, not to be talked out of approaching.
        AttachPose? pose = null;
        if (rider.AttachPoses != null && !this.TryPickFreePose(rider.AttachPoses, out pose)) { return false; }
        // A rider with no pose SET but a DefaultPose still rides pose art, so it must seat like a pose
        // rider — its clips are drawn for the pose's placement. Resolving the fallback only for clips
        // and not for seating would render that art at a scattered anchor instead of its authored
        // offset. The default is deliberately outside the free-pose pool: it is a fallback, so several
        // pose-less riders may share it, where set poses stay exclusive.
        pose ??= rider.DefaultPose;

        var footprint = Mathf.Max(rider.Footprint, 0f);
        var occupied = new List<Vector3>(this._records.Count);
        foreach (var existing in this._records) { occupied.Add(existing.LocalAnchor); }

        var anchor = this.AnchorProfile.Place(this.MeasureBounds(), occupied, footprint, this._rng.GetRndFloat);
        // A pose rider rides at its authored offset (origin by default) and only ever spends its anchor
        // on fling spread, so a congested silhouette must not refuse it; a pose-less rider has nowhere
        // else to sit. The origin-vs-anchor decision stays HERE, where a future per-host placement
        // strategy has exactly one site to replace.
        if (anchor == null && pose == null) { return false; }

        var localAnchor = pose != null ? pose.PoseOffset : (anchor ?? Vector3.Zero);
        record = new AttachmentRecord(
            rider,
            this._nextAttachSequence++,
            Mathf.Max(rider.MaxGrip, 0f),
            footprint,
            localAnchor,
            AttachmentPhase.Reserved,
            pose);

        this._records.Add(record);
        this.SubscribeTreeExiting(rider);
        rider.OnReserved(this, localAnchor);
        return true;
    }

    /// <inheritdoc />
    public bool HasRoomFor(IAttachmentRider rider) => rider != null && this.CanSeat(rider);

    /// <summary>
    /// The feasibility gates every seating decision shares, side-effect-free so the public peek and the
    /// booking cannot drift apart. Deliberately NOT covering pose availability or anchor congestion:
    /// those consume state (the pose ledger, the RNG) or are refusals a committing caller absorbs.
    /// </summary>
    private bool CanSeat(IAttachmentRider rider)
    {
        // Tree-walk discovery finds this component whether or not its init ran, and _rng is null
        // until it does.
        if (!this.IsInitialized) { return false; }
        if (!IsRiderAlive(rider)) { return false; }
        // Booked on another host. A second record would double-book its footprint and leave the losing
        // host holding a rider that answers to someone else.
        if (rider.Host != null) { return false; }

        var footprint = Mathf.Max(rider.Footprint, 0f);
        var bounds = this.MeasureBounds();
        var capacity = this.CapacityProvider.GetCapacity(bounds, this._stats, this._entity);
        if (capacity <= 0f && !bounds.IsMeasured) { this.WarnUnmeasuredBounds(); }

        // Float epsilon: a rider whose footprint exactly fills the remaining budget must fit.
        return this.UsedFootprint + footprint <= capacity + 0.0001f;
    }

    /// <summary>
    /// Pick one of <paramref name="set"/>'s poses no rider on THIS host currently holds, uniformly at
    /// random from the free ones off the same seeded stream anchor placement draws from. False when every
    /// id is taken — the pose set, not mechanical capacity, is usually the binding parallel limit.
    /// </summary>
    private bool TryPickFreePose(AttachPoseSet set, out AttachPose? pose)
    {
        pose = null;
        var poses = set.ValidatedPoses;

        List<AttachPose>? free = null;
        foreach (var candidate in poses)
        {
            if (this.IsPoseHeld(candidate.Id)) { continue; }

            free ??= new List<AttachPose>(poses.Count);
            free.Add(candidate);
        }

        if (free == null) { return false; }

        pose = free[this._rng.GetRndInt(free.Count)];
        return true;
    }

    /// <summary>
    /// Occupancy is keyed by <see cref="AttachPose.Id"/>, never by resource instance: a set loaded under
    /// <c>CacheMode.Ignore</c> or inlined into a PackedScene yields different instances of the same
    /// authored pose, and instance keying would silently let both ride the same visual.
    /// </summary>
    private bool IsPoseHeld(StringName id)
    {
        foreach (var record in this._records)
        {
            if (record.Pose != null && record.Pose.Id == id) { return true; }
        }

        return false;
    }

    /// <inheritdoc />
    public void ConfirmAttach(IAttachmentRider rider)
    {
        if (rider == null) { return; }

        var index = this.IndexOf(rider);
        if (index < 0)
        {
            JmoLogger.Warning(this, "[Attachment] ConfirmAttach for a rider this host holds no reservation for — " +
                "the reservation was already handed back, so the arrival is dropped.");
            return;
        }

        this._records[index] = this._records[index] with { Phase = AttachmentPhase.Riding };
        rider.OnAttached(this, this._records[index].LocalAnchor);
    }

    /// <inheritdoc />
    public void Detach(IAttachmentRider rider, DetachCause cause)
    {
        if (rider == null) { return; }
        if (!this.ReleaseRecord(rider, cause)) { return; }
        if (!IsRiderAlive(rider)) { return; }

        rider.OnDetached(cause);
    }

    /// <inheritdoc />
    public ShedPlan ApplyShed(ShedRequest request)
    {
        if (request == null) { return ShedPlan.Empty; }

        this.ReleaseDeadRecords();
        if (this._records.Count == 0) { return ShedPlan.Empty; }

        // Reservations are filtered out before the resolver sees them: a rider still in flight grips
        // nothing, so force spent against it would buy the attacker nothing and tear down a
        // reservation the blow never reached.
        var riding = new List<AttachmentRecord>(this._records.Count);
        foreach (var record in this._records)
        {
            if (record.Phase == AttachmentPhase.Riding) { riding.Add(record); }
        }

        if (riding.Count == 0) { return ShedPlan.Empty; }

        var plan = AttachmentShedResolver.Resolve(riding, request.Force, request.Scope);
        var attribution = request.Instigator ?? this.GetUnderlyingNode();

        // Aim before anything is removed — the anchor is gone once the record is. Resolved for every
        // outcome, not just the shed ones: a rider that only took damage still hands its direction to
        // the hit, and that is the same aim its fling would have used.
        var directions = new Dictionary<IAttachmentRider, Vector3>();
        foreach (var outcome in plan.Outcomes)
        {
            directions[outcome.Record.Rider] =
                this.FlingDirectionFor(outcome.Record, request.OriginPosition, request.ImpactDirection);
        }

        this.WriteBackGrip(plan);

        if (request.DamagePayload != null)
        {
            foreach (var outcome in plan.Damaged)
            {
                // Shed riders take the payload here — the shed is the deterministic damage path for
                // a swing that shakes a rider off. A swing whose hitbox ALSO overlaps the rider (one
                // riding the attacker's own host sits inside the hitbox's reach) must suppress that
                // direct hit itself, through the hitbox-side IPayloadInterceptor3D seam, or the two
                // applications stack into a double hit.
                if (!IsRiderAlive(outcome.Record.Rider)) { continue; }

                // The fling takes the resolved direction because it always needs one and falls back to a
                // constant; the hit takes the raw authored one because its consumers read null as "nothing
                // was aimed, infer it yourself".
                outcome.Record.Rider.TryApplyShedDamage(request.DamagePayload, request.ImpactDirection);
            }
        }

        foreach (var outcome in plan.Shed)
        {
            var rider = outcome.Record.Rider;
            this.ReleaseRecord(rider, DetachCause.Shed);

            // A rider the damage killed has already detached itself and is NOT flung — the guard
            // below skips it. Its fling direction was resolved above only to aim the hit it took.
            if (!IsRiderAlive(rider)) { continue; }

            rider.OnShed(directions[rider], outcome.ForceSpent, attribution);
        }

        return plan;
    }

    /// <inheritdoc />
    public bool TryGetAnchorWorldPosition(IAttachmentRider rider, out Vector3 worldPosition)
    {
        worldPosition = Vector3.Zero;
        var index = this.IndexOf(rider);
        if (index < 0) { return false; }
        if (!GodotObject.IsInstanceValid(this._entity) || !this._entity.IsInsideTree()) { return false; }

        // Pose art is drawn on a host-sized canvas with the rider already at the right body spot, so a
        // posed rider rides the host's own origin — applying the anchor too would double the offset the
        // art already bakes in. LocalAnchor keeps its fling-direction role either way.
        worldPosition = this._records[index].Pose != null
            ? this._entity.GlobalPosition
            : this._entity.ToGlobal(this._records[index].LocalAnchor);
        return true;
    }

    /// <summary>
    /// Release every rider on the roster, newest first. Iterated off a SNAPSHOT: <see cref="Detach"/>
    /// re-enters consumer code through <see cref="RiderDetached"/>, which can release other riders and
    /// reshape the live list mid-loop.
    /// </summary>
    private void DetachAll(DetachCause cause)
    {
        var riders = new List<IAttachmentRider>(this._records.Count);
        foreach (var record in this._records) { riders.Add(record.Rider); }

        for (var i = riders.Count - 1; i >= 0; i--)
        {
            this.Detach(riders[i], cause);
        }
    }

    private void ReleaseDeadRecords()
    {
        // Collected before any release, for the same re-entrancy reason as DetachAll. Allocation-free
        // on the overwhelmingly common no-dead-riders frame.
        List<IAttachmentRider>? dead = null;
        foreach (var record in this._records)
        {
            if (IsRiderAlive(record.Rider)) { continue; }

            dead ??= new List<IAttachmentRider>();
            dead.Add(record.Rider);
        }

        if (dead == null) { return; }

        foreach (var rider in dead) { this.ReleaseRecord(rider, DetachCause.RiderRemoved); }
    }

    /// <summary>
    /// Drop <paramref name="rider"/>'s record and every piece of bookkeeping that hangs off it, then
    /// announce the release. The one place a record leaves the roster — the record is removed BEFORE
    /// the event fires, because a consumer's own release path calls back in and would otherwise recurse.
    /// </summary>
    /// <returns>False when this host held no record for the rider.</returns>
    private bool ReleaseRecord(IAttachmentRider rider, DetachCause cause)
    {
        var index = this.IndexOf(rider);
        if (index < 0) { return false; }

        this._records.RemoveAt(index);
        this.UnsubscribeTreeExiting(rider);
        this.RiderDetached.Invoke(rider, cause);
        return true;
    }

    /// <summary>
    /// Shed the overload when the host shrinks. A bounds-derived capacity is recomputed live, so a
    /// host whose art got smaller would otherwise carry more footprint than it can hold forever.
    /// Reservations are evicted before riders: nothing has arrived to lose. Runs on the
    /// <see cref="CapacitySweepInterval"/> cadence, not every frame.
    /// </summary>
    private void EnforceCapacity()
    {
        if (this._records.Count == 0) { return; }

        var capacity = this.Capacity;
        while (this._records.Count > 0 && this.UsedFootprint > capacity + 0.0001f)
        {
            this.Detach(this._records[this.WeakestClaimIndex()].Rider, DetachCause.CapacityRevoked);
        }
    }

    private int WeakestClaimIndex()
    {
        var weakest = 0;
        for (var i = 1; i < this._records.Count; i++)
        {
            if (IsWeakerClaim(this._records[i], this._records[weakest])) { weakest = i; }
        }

        return weakest;
    }

    /// <summary>
    /// Eviction order: every reservation before any rider, newest reservation first (it has the least
    /// flight invested), then riders weakest-remaining-grip first with ties by attach sequence — the
    /// same ordering <see cref="AttachmentShedResolver"/> spends force in.
    /// </summary>
    private static bool IsWeakerClaim(AttachmentRecord candidate, AttachmentRecord incumbent)
    {
        if (candidate.Phase != incumbent.Phase) { return candidate.Phase == AttachmentPhase.Reserved; }
        if (candidate.Phase == AttachmentPhase.Reserved) { return candidate.AttachSequence > incumbent.AttachSequence; }
        if (!Mathf.IsEqualApprox(candidate.RemainingGrip, incumbent.RemainingGrip))
        {
            return candidate.RemainingGrip < incumbent.RemainingGrip;
        }

        return candidate.AttachSequence < incumbent.AttachSequence;
    }

    private void WriteBackGrip(ShedPlan plan)
    {
        foreach (var outcome in plan.Outcomes)
        {
            var index = this.IndexOf(outcome.Record.Rider);
            if (index < 0) { continue; }

            this._records[index] = this._records[index] with { RemainingGrip = outcome.RemainingGrip };
        }
    }

    private Vector3 FlingDirectionFor(AttachmentRecord record, Vector3 origin, Vector3? impactDirection)
    {
        var anchorWorld = GodotObject.IsInstanceValid(this._entity) && this._entity.IsInsideTree()
            ? this._entity.ToGlobal(record.LocalAnchor)
            : record.LocalAnchor;

        return AttachmentShedResolver.ResolveFlingDirection(anchorWorld, origin, impactDirection);
    }

    private VisualBounds3D MeasureBounds()
    {
        // Measured per call rather than cached: an entity's art changes with size controllers and
        // animation state. The capacity sweep only measures while riders are actually held, and only
        // on its throttled cadence.
        return EntityVisualBounds3D.Measure(this._entity);
    }

    private void WarnUnmeasuredBounds()
    {
        if (this._warnedUnmeasuredBounds) { return; }

        this._warnedUnmeasuredBounds = true;
        JmoLogger.Warning(this, "[Attachment] This host's silhouette could not be measured, so its capacity " +
            "resolves to 0 and every rider is refused. Check that the entity has drawn art inside the tree.");
    }

    private int IndexOf(IAttachmentRider rider)
    {
        for (var i = 0; i < this._records.Count; i++)
        {
            if (ReferenceEquals(this._records[i].Rider, rider)) { return i; }
        }

        return -1;
    }

    private void SubscribeTreeExiting(IAttachmentRider rider)
    {
        if (this._treeExitHandlers.ContainsKey(rider)) { return; }
        if (rider.GetUnderlyingNode() is not Node node) { return; }

        void Handler() => this.Detach(rider, DetachCause.RiderRemoved);
        this._treeExitHandlers[rider] = Handler;
        node.TreeExiting += Handler;
    }

    private void UnsubscribeTreeExiting(IAttachmentRider rider)
    {
        if (!this._treeExitHandlers.Remove(rider, out var handler)) { return; }
        if (rider.GetUnderlyingNode() is not Node node) { return; }
        if (!GodotObject.IsInstanceValid(node)) { return; }

        node.TreeExiting -= handler;
    }

    /// <summary>
    /// A rider is alive only while its node is valid AND nothing in its ancestry is queued for
    /// deletion. Two traps stack here: <see cref="GodotObject.IsInstanceValid"/> still answers true
    /// for a queued node, and <c>QueueFree</c> stamps the flag on the node it was called on ONLY —
    /// a rider component whose ENTITY is being freed reports false for its own queued state.
    /// </summary>
    private static bool IsRiderAlive(IAttachmentRider rider)
    {
        if (rider.GetUnderlyingNode() is not Node node) { return false; }
        if (!GodotObject.IsInstanceValid(node)) { return false; }

        for (Node? ancestor = node; ancestor != null; ancestor = ancestor.GetParent())
        {
            if (ancestor.IsQueuedForDeletion()) { return false; }
        }

        return true;
    }

    private HurtboxComponent3D? _hurtbox;

    /// <summary>
    /// Route a rider's per-second ride damage through this host's OWN hurtbox, so armour, reaction
    /// resolvers, payload interceptors and i-frames all run. The host performs the call rather than the
    /// rider reaching across into another entity's components — the mirror of
    /// <see cref="IAttachmentRider.TryApplyShedDamage"/>, and for the same reason: a hurtbox is resolved
    /// from its owning entity's blackboard.
    /// </summary>
    /// <returns>True when the hurtbox processed the hit; false when it rejected it or none exists.</returns>
    public bool TryApplyRideDamage(IAttackPayload payload)
    {
        if (payload == null) { return false; }
        if (this._hurtbox == null || !GodotObject.IsInstanceValid(this._hurtbox)) { return false; }

        return this._hurtbox.ProcessHit(payload);
    }

    #region IComponent

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    public bool Initialize(IBlackboard bb)
    {
        if (this.CapacityProvider == null)
        {
            JmoLogger.Error(this, "[Attachment] No CapacityProvider authored — this host can carry nothing.");
            return false;
        }

        if (this.AnchorProfile == null)
        {
            JmoLogger.Error(this, "[Attachment] No AnchorProfile authored — this host can seat nobody.");
            return false;
        }

        this._entity = bb.TryGet<Node3D>(BBDataSig.Agent, out var agent) && agent != null ? agent : this;
        bb.TryGet<IStatProvider>(BBDataSig.Stats, out this._stats);
        bb.TryGet<HurtboxComponent3D>(BBDataSig.HurtboxComponent, out this._hurtbox);
        this._rng = EntityRngResolver.Resolve(bb, SeedKinds.Attachment, this, ref this._warnedMissingSeed);

        IsInitialized = true;
        Initialized();
        return true;
    }

    public void OnPostInitialize()
    {
        ProcessMode = ProcessModeEnum.Inherit;

        if (this._hurtbox == null)
        {
            JmoLogger.Warning(this, "[Attachment] No HurtboxComponent3D on the blackboard — riders will deal " +
                "no ride damage to this host.");
        }
    }

    public Node GetUnderlyingNode() => this;

    #endregion

    #region Test Helpers
#if TOOLS

    internal bool _TestSuppressExitTreeDetach { get; set; }

    internal void SetCapacityProvider(AttachmentCapacityProvider3D provider) => this.CapacityProvider = provider;

    internal void SetAnchorProfile(AttachmentAnchorProfile3D profile) => this.AnchorProfile = profile;

#endif
    #endregion
}
