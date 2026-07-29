namespace Jmodot.Implementation.Interaction.Attachment;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Components;
using Jmodot.Core.Interaction;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.AI.BB;
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

    private readonly List<AttachmentRecord> _records = new();
    private readonly Dictionary<IAttachmentRider, Action> _treeExitHandlers = new();

    private IStatProvider? _stats;
    private Node3D _entity = null!;
    private JmoRng _rng = null!;
    private bool _warnedMissingSeed;
    private int _nextAttachSequence;

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
    public event Action<IAttachmentRider, DetachCause> RiderDetached = delegate { };

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        ProcessMode = ProcessModeEnum.Disabled;
    }

    public override void _PhysicsProcess(double delta)
    {
        this.ReleaseDeadRecords();
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
    public bool TryAttach(IAttachmentRider rider, out AttachmentRecord record)
    {
        record = default;
        if (rider == null) { return false; }
        if (!IsRiderAlive(rider)) { return false; }
        // Already riding — this host or another. A second record would double-book its footprint
        // and leave the losing host holding a rider that answers to someone else.
        if (rider.IsAttached || this.IndexOf(rider) >= 0) { return false; }

        var footprint = Mathf.Max(rider.Footprint, 0f);
        // Float epsilon: a rider whose footprint exactly fills the remaining budget must fit.
        if (this.UsedFootprint + footprint > this.Capacity + 0.0001f) { return false; }

        var bounds = this.MeasureBounds();
        var occupied = new List<Vector3>(this._records.Count);
        foreach (var existing in this._records) { occupied.Add(existing.LocalAnchor); }

        var anchor = AttachmentAnchorPlacer.Place(bounds, occupied, footprint, this._rng.GetRndFloat);
        if (anchor == null) { return false; }

        record = new AttachmentRecord(
            rider,
            this._nextAttachSequence++,
            Mathf.Max(rider.MaxGrip, 0f),
            footprint,
            anchor.Value);

        this._records.Add(record);
        this.SubscribeTreeExiting(rider);
        rider.OnAttached(this, anchor.Value);
        return true;
    }

    /// <inheritdoc />
    public void Detach(IAttachmentRider rider, DetachCause cause)
    {
        if (rider == null) { return; }

        var index = this.IndexOf(rider);
        if (index < 0) { return; }

        // Remove BEFORE notifying: the rider's own release path calls back into Detach, and a
        // record still on the roster would recurse.
        this._records.RemoveAt(index);
        this.UnsubscribeTreeExiting(rider);

        this.RiderDetached.Invoke(rider, cause);
        if (!IsRiderAlive(rider)) { return; }

        rider.OnDetached(cause);
    }

    /// <inheritdoc />
    public ShedPlan ApplyShed(ShedRequest request)
    {
        if (request == null) { return ShedPlan.Empty; }

        this.ReleaseDeadRecords();
        if (this._records.Count == 0) { return ShedPlan.Empty; }

        var plan = AttachmentShedResolver.Resolve(this._records, request.Force, request.Scope);

        // Aim every fling before anything is removed — the anchor is gone once the record is.
        var directions = new Dictionary<IAttachmentRider, Vector3>();
        foreach (var outcome in plan.Shed)
        {
            directions[outcome.Record.Rider] = this.FlingDirectionFor(outcome.Record, request.OriginPosition);
        }

        this.WriteBackGrip(plan);

        if (request.DamagePayload != null)
        {
            foreach (var outcome in plan.Damaged)
            {
                if (!IsRiderAlive(outcome.Record.Rider)) { continue; }
                outcome.Record.Rider.TryApplyShedDamage(request.DamagePayload);
            }
        }

        foreach (var outcome in plan.Shed)
        {
            var rider = outcome.Record.Rider;
            var index = this.IndexOf(rider);
            if (index >= 0)
            {
                this._records.RemoveAt(index);
                this.UnsubscribeTreeExiting(rider);
                this.RiderDetached.Invoke(rider, DetachCause.Shed);
            }

            // A rider killed by the damage above has already detached itself; it is still flung,
            // so a corpse carries the momentum of the blow that killed it.
            if (!IsRiderAlive(rider)) { continue; }

            rider.OnShed(directions[rider], outcome.ForceSpent);
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

        worldPosition = this._entity.ToGlobal(this._records[index].LocalAnchor);
        return true;
    }

    /// <summary>Release every rider currently on the roster, newest first so no index shifts under the loop.</summary>
    private void DetachAll(DetachCause cause)
    {
        for (var i = this._records.Count - 1; i >= 0; i--)
        {
            this.Detach(this._records[i].Rider, cause);
        }
    }

    private void ReleaseDeadRecords()
    {
        for (var i = this._records.Count - 1; i >= 0; i--)
        {
            if (IsRiderAlive(this._records[i].Rider)) { continue; }

            var rider = this._records[i].Rider;
            this._records.RemoveAt(i);
            this._treeExitHandlers.Remove(rider);
            this.RiderDetached.Invoke(rider, DetachCause.RiderRemoved);
        }
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

    private Vector3 FlingDirectionFor(AttachmentRecord record, Vector3 origin)
    {
        var anchorWorld = GodotObject.IsInstanceValid(this._entity) && this._entity.IsInsideTree()
            ? this._entity.ToGlobal(record.LocalAnchor)
            : record.LocalAnchor;

        var away = anchorWorld - origin;
        // A rider sitting exactly on the origin has no direction to be pushed; Back is a stable,
        // horizontal fallback (knockback flattens Y, so Up would resolve to no impulse at all).
        return away.IsZeroApprox() ? Vector3.Back : away.Normalized();
    }

    private VisualBounds3D MeasureBounds()
    {
        // Measured per call rather than cached: an entity's art changes with size controllers and
        // animation state, and attachment is a rare event, not a per-frame cost.
        return EntityVisualBounds3D.Measure(this._entity);
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
    /// A rider is only alive while its node is both valid AND not already queued for deletion —
    /// <see cref="GodotObject.IsInstanceValid"/> alone still answers true for a queued node.
    /// </summary>
    private static bool IsRiderAlive(IAttachmentRider rider)
    {
        if (rider.GetUnderlyingNode() is not Node node) { return false; }
        return GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
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

        this._entity = bb.TryGet<Node3D>(BBDataSig.Agent, out var agent) && agent != null ? agent : this;
        bb.TryGet<IStatProvider>(BBDataSig.Stats, out this._stats);
        this._rng = EntityRngResolver.Resolve(bb, SeedKinds.Attachment, this, ref this._warnedMissingSeed);

        IsInitialized = true;
        Initialized();
        return true;
    }

    public void OnPostInitialize()
    {
        ProcessMode = ProcessModeEnum.Inherit;
    }

    public Node GetUnderlyingNode() => this;

    #endregion

    #region Test Helpers
#if TOOLS

    internal bool _TestSuppressExitTreeDetach { get; set; }

    internal void SetCapacityProvider(AttachmentCapacityProvider3D provider) => this.CapacityProvider = provider;

#endif
    #endregion
}
