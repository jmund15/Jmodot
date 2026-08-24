namespace Jmodot.Implementation.Body.Segmented;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.AI.BB;
using Core.Combat;
using Core.Components;
using Core.Health;
using Core.Shared.Attributes;
using Implementation.AI.BB;
using Implementation.Health;
using Implementation.Physics;
using Implementation.Shared;
using Implementation.Shared.GodotExceptions;

/// <summary>
/// Grows and drives the train of units trailing a head entity. It instances them from one authored
/// scene, places them as world-space siblings of the head along the head's own recorded path, and
/// owns their lifetime.
/// </summary>
/// <remarks>
/// <para>
/// Author this as a DIRECT child of the head scene's root; the root is the pose this chain follows
/// and its parent is where the units are placed. Units are never reparented under the head, so the
/// head's subtree still contains exactly one health and the tail is not freed with the head.
/// </para>
/// <para>
/// Sampling and placement run on the idle frame, which always follows the physics frame that moved
/// the head, so no execution-order contract exists for an author to get wrong. The consequence is a
/// one-physics-tick lag on unit hurtbox positions.
/// </para>
/// <para>
/// <see cref="_Ready"/> self-populates ONLY when the chain holds no units yet, so a chain handed a
/// tail before it enters the tree keeps that tail instead of growing a fresh one on top of it.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class SegmentedBodyComponent3D : Node3D, IComponent, IBlackboardProvider, IEntityBodyGraph
{
    /// <summary>The scene each unit is instanced from. One home per species.</summary>
    [Export, RequiredExport] public PackedScene SegmentScene { get; private set; } = null!;

    /// <summary>Metres between unit centres. Match it to the unit art's extent — a mismatch gaps or overlaps the body on sight.</summary>
    [Export] public float SegmentSpacing { get; private set; } = 0.5f;

    /// <summary>Fewest units a fresh body starts with. Clamped into [1, MaxSegments].</summary>
    [Export] public int InitialSegmentsMin { get; private set; } = 1;

    /// <summary>Most units a fresh body starts with. Clamped into [1, MaxSegments].</summary>
    [Export] public int InitialSegmentsMax { get; private set; } = 5;

    /// <summary>Hard ceiling on units, so total body length caps at MaxSegments + 1.</summary>
    [Export] public int MaxSegments { get; private set; } = 11;

    /// <summary>Shortest total body — head included — that stays alive. Never below 2.</summary>
    [Export] public int MinLength { get; private set; } = 2;

    private readonly List<BodySegment3D> _segments = new();
    private readonly Dictionary<BodySegment3D, Action> _exitHooks = new();
    private readonly List<BodySegment3D> _resolutionRoster = new();
    private readonly HashSet<int> _deadIndices = new();

    // A body placed on the frame its room is built probes a physics world that has not published
    // that room's static bodies yet, so the first few frames can honestly find no floor. Spawning
    // waits them out rather than laying the body flat over ground it simply could not see.
    private const int SpawnGroundProbeFrames = 8;

    // How far above a promotion front unit the buried-discrimination probe starts: high enough to
    // clear a floor slab the unit slipped under, low enough not to read an upper storey as "above".
    private const float BuriedProbeHeight = 3f;

    private PositionHistory? _history;
    private Node3D? _head;
    private HealthComponent? _headHealth;
    private Vector3 _headFacing = Vector3.Forward;
    private int _spawnProbesLeft;
    private bool _resolutionOpen;
    private bool _resolutionQueued;
    private bool _headDead;
    private bool _promotionGroundRetryPending;
    private int _progenyOrdinal;

    /// <summary>The units this chain currently owns, front-first.</summary>
    public IReadOnlyList<BodySegment3D> Segments => this._segments;

    /// <inheritdoc />
    public IEnumerable<Node> BodyGraphNodes => this._segments.Select(segment => segment.Body);

    /// <summary>Total body length: the units plus the head.</summary>
    public int Length => this._segments.Count + 1;

    /// <inheritdoc />
    public (StringName Key, object Value)? Provision => (BBDataSig.SegmentedBody, this);

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { return; }

        this.ValidateRequiredExports();
        this.ValidateRanges();
        this._head = this.GetParent() as Node3D
            ?? throw new NodeConfigurationException(
                $"{nameof(SegmentedBodyComponent3D)} must be a direct child of the head scene's Node3D root.", this);

        this._history = new PositionHistory(this.MaxSegments, this.SegmentSpacing);
        if (this._segments.Count > 0)
        {
            this.ReseedFromCurrentPoses();
            return;
        }

        // NOT spawned here: this runs inside the AddChild that puts the head in the tree, which is
        // strictly before a spawner writes the head's placement onto it. A body laid out now is laid
        // out around wherever the head scene happens to sit — for an encounter spawn, its container's
        // origin — and nothing afterwards moves it back.
        this._spawnProbesLeft = SpawnGroundProbeFrames;
    }

    public override void _EnterTree()
    {
        // Re-armed here rather than at bind time so the hooks survive a reparent of the head entity.
        foreach (var segment in this._segments)
        {
            this.HookSegment(segment);
            if (segment.Body.IsInsideTree()) { NormalizeWorldBasis(segment.Body); }
        }
    }

    public override void _ExitTree()
    {
        foreach (var segment in this._segments) { this.UnhookSegment(segment); }

        // A head being deleted is unambiguous, and taking the body with it there keeps teardown off the
        // deferred queue entirely. Everything else — reparenting most of all — also fires _ExitTree, so
        // it waits a frame and frees only a chain that is still absent.
        if (this.IsQueuedForDeletion()
            || this._head == null
            || !GodotObject.IsInstanceValid(this._head)
            || this._head.IsQueuedForDeletion())
        {
            this.FreeSegments();
            return;
        }

        Callable.From(this.FreeSegmentsIfRemoved).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) { return; }
        if (this._head == null || this._history == null) { return; }
        if (this._spawnProbesLeft > 0) { this.TrySpawnInitialSegments(); }
        if (this._segments.Count == 0) { return; }

        var headPosition = this._head.GlobalPosition;
        var travelled = headPosition - this._history.SampleAtDistance(0f).position;
        if (!travelled.IsZeroApprox()) { this._headFacing = travelled.Normalized(); }

        this._history.TryAppend(headPosition, this._headFacing);

        for (var k = 0; k < this._segments.Count; k++)
        {
            var (position, facing) = this._history.SampleAtDistance((k + 1) * this.SegmentSpacing);
            var segment = this._segments[k];
            segment.Body.GlobalPosition = position;
            segment.Facing = facing;
        }
    }

    /// <summary>
    /// Raised as each unit dies, carrying the index it held in the roster this frame's split will be
    /// resolved against. That index is meaningful only until the split runs — hold the unit itself,
    /// never the number.
    /// </summary>
    public event Action<BodySegment3D, int>? SegmentDied;

    /// <summary>
    /// Raised as each unit joins the roster — initial spawn, adoption from a severed run, a
    /// promoted head taking its tail. Watchers of per-unit state subscribe here rather than
    /// scanning at init: the initial body spawns FRAMES after component init (ground-probe
    /// deferral), so an init-time scan sees an empty roster.
    /// </summary>
    public event Action<BodySegment3D, int>? SegmentAdopted;

    /// <summary>
    /// Raised once when a split leaves the head with a body shorter than it can live as, immediately
    /// before the head kills itself through its own health — which is the kill an encounter counts.
    /// </summary>
    public event Action? BelowMinimumLength;

    /// <summary>
    /// Raised once per body promoted out of this one, after it is in the tree and initialized. The
    /// mechanism is complete without a subscriber; a listener adds tracking, never correctness.
    /// </summary>
    public event Action<Node3D>? ProgenySpawned;

    /// <summary>
    /// Hands this chain a run of units to own, front first, re-indexing them behind its head.
    /// Callable BEFORE the chain enters the tree, and that is the point: a chain that already holds
    /// units when it readies skips its own initial spawn, so a promoted body wears the tail it
    /// inherited instead of growing a fresh one on top of it.
    /// </summary>
    public void AdoptSegments(IReadOnlyList<BodySegment3D> tail)
    {
        if (tail == null) { return; }

        var adopted = new List<BodySegment3D>(tail.Count);
        foreach (var segment in tail)
        {
            if (segment == null || !GodotObject.IsInstanceValid(segment)) { continue; }
            if (this._segments.Contains(segment)) { continue; }

            this._segments.Add(segment);
            if (segment.Body.IsInsideTree()) { NormalizeWorldBasis(segment.Body); }
            segment.Died += this.OnSegmentDied;
            this.HookSegment(segment);
            adopted.Add(segment);
        }

        this.Reindex();
        foreach (var segment in adopted)
        {
            this.SegmentAdopted?.Invoke(segment, this._segments.IndexOf(segment));
        }
    }

    /// <summary>
    /// Kills the given units, joining them to this frame's single split resolution. Takes the units
    /// themselves rather than indices, because an index stops meaning anything the moment another
    /// unit in the same frame dies.
    /// </summary>
    public void KillSegments(IEnumerable<BodySegment3D> segments, object source)
    {
        if (segments == null) { return; }

        foreach (var segment in new List<BodySegment3D>(segments))
        {
            if (segment == null || !GodotObject.IsInstanceValid(segment)) { continue; }
            if (!this._segments.Contains(segment)) { continue; }
            if (segment.Health is HealthComponent health && !health.IsDead) { health.Kill(source); }
        }
    }

    /// <summary>
    /// Tells the chain its head was warped rather than moved. Re-seeds the trail from where the body
    /// stands right now, so the units hold their shape instead of snapping onto the head.
    /// </summary>
    public void NotifyTeleported() => this.ReseedFromCurrentPoses();

    #region IComponent

    public bool IsInitialized { get; private set; }

    public event Action Initialized = delegate { };

    /// <exception cref="NodeConfigurationException">The head's blackboard carries no health.</exception>
    public bool Initialize(IBlackboard bb)
    {
        this.ValidateRanges();

        // Teardown-first: every phase may run again on the same instance (scene rebind, pool reuse).
        if (this._headHealth != null) { this._headHealth.OnDied -= this.OnHeadDied; }

        if (!bb.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health) || health == null)
        {
            throw new NodeConfigurationException(
                "A segmented body requires a HealthComponent on its head's own blackboard: a body cut below its shortest living length kills its head through it.",
                this);
        }

        this._headHealth = health;
        this.IsInitialized = true;
        this.Initialized();
        return true;
    }

    public void OnPostInitialize()
    {
        if (this._headHealth == null) { return; }

        this._headHealth.OnDied -= this.OnHeadDied;
        this._headHealth.OnDied += this.OnHeadDied;
    }

    public Node GetUnderlyingNode() => this;

    #endregion

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (this.SegmentScene == null)
        {
            warnings.Add("SegmentScene is unset — this chain has nothing to instance.");
        }

        warnings.AddRange(this.RangeRuleViolations());

        if (this.GetParent() is not Node3D)
        {
            warnings.Add("This chain must be a direct child of the head scene's Node3D root.");
        }

        var missingHealth = ConfigWarnings.RequireEntitySibling<HealthComponent>(this,
            "No HealthComponent on this head — a body cut below its shortest living length has nothing to die through.");
        if (missingHealth != null)
        {
            warnings.Add(missingHealth);
        }

        return warnings.Concat(base._GetConfigurationWarnings() ?? []).ToArray();
    }

    /// <summary>
    /// The ONE home for the four range rules: the editor warnings and the load-time throws both
    /// consume this list, so the two tiers can never silently disagree about what is legal.
    /// </summary>
    private List<string> RangeRuleViolations()
    {
        var violations = new List<string>();
        if (this.MinLength < 2)
        {
            violations.Add("MinLength must be at least 2: a body shorter than a head plus one unit is just the head.");
        }

        if (this.InitialSegmentsMin > this.InitialSegmentsMax)
        {
            violations.Add("InitialSegmentsMin must not exceed InitialSegmentsMax.");
        }

        if (this.InitialSegmentsMax > this.MaxSegments)
        {
            violations.Add("InitialSegmentsMax must not exceed MaxSegments — a fresh body could never reach it.");
        }

        if (this.MaxSegments < this.MinLength - 1)
        {
            violations.Add("MaxSegments must be at least MinLength - 1 — a full-length body would already be under its death threshold.");
        }

        return violations;
    }

    /// <exception cref="NodeConfigurationException">Any authored range is self-contradictory.</exception>
    private void ValidateRanges()
    {
        var violations = this.RangeRuleViolations();
        if (violations.Count > 0)
        {
            throw new NodeConfigurationException(string.Join(" ", violations), this);
        }
    }

    /// <summary>
    /// Spawns the body once the head's spawn pose is final and the ground under it is readable,
    /// giving up on the ground after <see cref="SpawnGroundProbeFrames"/> frames and laying the body
    /// out flat rather than leaving the head bodiless.
    /// </summary>
    private void TrySpawnInitialSegments()
    {
        this._spawnProbesLeft--;

        // Only a head that HAS a ground contract can be waiting on physics to publish one. A head
        // without one never resolves ground at any point, so making it wait would delay every body
        // that legitimately stands on nothing.
        if (this._spawnProbesLeft > 0
            && this._head is PhysicsBody3D
            && !this.TryProbeGround(this._head.GlobalPosition, out _))
        {
            return;
        }

        this._spawnProbesLeft = 0;
        this.SpawnInitialSegments();
    }

    private void SpawnInitialSegments()
    {
        var head = this._head!;
        var container = head.GetParent()
            ?? throw new NodeConfigurationException(
                "The head entity has no parent, so its body units have nowhere to be placed as siblings.", this);

        var forward = -head.GlobalTransform.Basis.Z;
        forward = forward.IsZeroApprox() ? Vector3.Forward : forward.Normalized();
        this._headFacing = forward;

        var count = Math.Clamp(this.DrawInitialCount(), 1, this.MaxSegments);
        var poses = ChainLayout.Resolve(head.GlobalPosition, forward, count, this.SegmentSpacing, this.TryProbeGround);

        for (var k = 0; k < count; k++)
        {
            var (position, facing) = poses[k + 1];
            var instance = this.SegmentScene.Instantiate<Node3D>();
            // Local pose before AddChild: a GlobalPosition write outside the tree is dropped.
            instance.Position = position;
            container.AddChild(instance);
            instance.GlobalPosition = position;
            NormalizeWorldBasis(instance);

            if (!instance.TryGetFirstChildOfType<BodySegment3D>(out var segment) || segment == null)
            {
                throw new NodeConfigurationException(
                    "SegmentScene carries no BodySegment3D, so the chain cannot drive what it instanced.", this);
            }

            this.Adopt(segment, k);
            segment.Facing = facing;
        }

        this._history!.Reseed(poses);

        // [DIAG-chain] one line per spawned chain: proves where every unit actually stands. A
        // "never saw a segment" report checks this line first — units at sane Y beside the head
        // means a RENDER problem; units missing/low/at origin means a PLACEMENT problem.
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        for (var k = 1; k < poses.Count; k++)
        {
            minY = Math.Min(minY, poses[k].position.Y);
            maxY = Math.Max(maxY, poses[k].position.Y);
        }
        JmoLogger.Info(this,
            $"[DIAG-chain] '{head.Name}' laid {count} units head={head.GlobalPosition} unitY=[{minY:F2},{maxY:F2}]");
    }

    /// <summary>
    /// The Y a unit standing at <paramref name="point"/> would have, resolved through the HEAD's own
    /// grounding: the head's collider decides the height and the head's collision mask decides what
    /// counts as ground, so a body needs no ground tunable of its own and cannot disagree with the
    /// creature it belongs to. A head that is not a physics body has no ground contract to borrow, so
    /// this always misses and the layout falls back to a flat chain.
    /// </summary>
    /// <summary>
    /// True when the walkable surface over <paramref name="frontPos"/>'s column sits ABOVE it — the
    /// unit is below the world and a promotion there falls out of bounds. A column with no surface at
    /// all (floorless world) is NOT buried, and a head with no physics grounding contract can never
    /// report buried.
    /// </summary>
    private bool IsFrontUnitBuried(Vector3 frontPos)
    {
        if (this._head is not PhysicsBody3D || !GodotObject.IsInstanceValid(this._head)
            || !this._head.IsInsideTree()) { return false; }
        if (this.TryProbeGround(frontPos, out _)) { return false; }

        return this.TryProbeGround(frontPos + (Vector3.Up * BuriedProbeHeight), out float surfaceY)
            && surfaceY > frontPos.Y;
    }

    private bool TryProbeGround(Vector3 point, out float standingY)
    {
        standingY = 0f;
        if (this._head is not PhysicsBody3D headBody) { return false; }
        return TryProbeGround(headBody, point, out standingY);
    }

    private static bool TryProbeGround(PhysicsBody3D body, Vector3 point, out float standingY)
    {
        standingY = 0f;
        if (!BodyGroundSnapper.TryGround(body, new Transform3D(Basis.Identity, point), out var grounded))
        {
            return false;
        }

        standingY = grounded.Origin.Y;
        return true;
    }

    private int DrawInitialCount()
    {
        var min = Math.Min(this.InitialSegmentsMin, this.InitialSegmentsMax);
        var max = Math.Max(this.InitialSegmentsMin, this.InitialSegmentsMax);
        return this.ResolveRng().GetRndInRange(min, max);
    }

    /// <summary>
    /// The head's own entity seed when its spawn source injected one before parenting. One home for
    /// the head→blackboard→EntitySeed walk; the two callers keep their own degradation policies
    /// (an unseeded draw vs a zero-plus-warning).
    /// </summary>
    private bool TryResolveHeadSeed(out int seed)
    {
        seed = 0;
        return this._head != null
            && GodotObject.IsInstanceValid(this._head)
            && this._head.TryGetFirstChildOfInterface<IBlackboard>(out var bb)
            && bb != null
            && bb.TryGet<int>(BBDataSig.EntitySeed, out seed);
    }

    /// <summary>
    /// A seeded draw when the head carries an entity seed, so a body's length replays; an unseeded
    /// draw otherwise, which is the same tier a scene-placed entity gets.
    /// </summary>
    private JmoRng ResolveRng()
        => this.TryResolveHeadSeed(out var seed) ? new JmoRng(seed) : JmoRng.UnseededByDesign();

    private static void NormalizeWorldBasis(Node3D root)
        => root.GlobalTransform = new Transform3D(Basis.Identity.Scaled(root.Scale), root.GlobalPosition);

    private void Adopt(BodySegment3D segment, int index)
    {
        this._segments.Insert(index, segment);
        segment.BindHost(this, index);
        segment.Died += this.OnSegmentDied;
        this.HookSegment(segment);
        this.Reindex();
        this.SegmentAdopted?.Invoke(segment, index);
    }

    private void HookSegment(BodySegment3D segment)
    {
        if (this._exitHooks.ContainsKey(segment)) { return; }

        Action handler = () => this.OnSegmentExiting(segment);
        this._exitHooks[segment] = handler;
        segment.TreeExiting += handler;
    }

    private void UnhookSegment(BodySegment3D segment)
    {
        if (!this._exitHooks.Remove(segment, out var handler)) { return; }
        if (!GodotObject.IsInstanceValid(segment)) { return; }

        segment.TreeExiting -= handler;
    }

    private void OnSegmentDied(BodySegment3D segment)
    {
        this.RecordDeath(segment);
        this.DropSegment(segment);
    }

    private void OnHeadDied(HealthChangeEventArgs _)
    {
        this.OpenResolution();
        this._headDead = true;
        this.QueueResolution();
    }

    /// <summary>
    /// The units adjacent to <paramref name="segment"/>, resolved against the roster the current
    /// frame's split will use: the frozen resolution roster while a death is resolving, the live
    /// roster otherwise. Death responders read adjacency HERE — by the time a death event reaches
    /// them, the dying unit is already off the live roster, so a live-roster search misses.
    /// </summary>
    public bool TryGetNeighbours(BodySegment3D segment, out BodySegment3D? previous, out BodySegment3D? next)
    {
        previous = null;
        next = null;
        var roster = this._resolutionOpen ? this._resolutionRoster : this._segments;
        var index = roster.IndexOf(segment);
        if (index < 0) { return false; }

        previous = index > 0 ? roster[index - 1] : null;
        next = index + 1 < roster.Count ? roster[index + 1] : null;
        return true;
    }

    private void RecordDeath(BodySegment3D segment)
    {
        this.OpenResolution();
        var index = this._resolutionRoster.IndexOf(segment);
        if (index < 0) { return; }

        this._deadIndices.Add(index);
        this.SegmentDied?.Invoke(segment, index);
        this.QueueResolution();
    }

    // The roster is frozen at the frame's FIRST death: every later index this frame addresses the
    // same numbering, so several deaths resolve as one split rather than shifting under each other.
    private void OpenResolution()
    {
        if (this._resolutionOpen) { return; }

        this._resolutionOpen = true;
        this._resolutionRoster.Clear();
        this._resolutionRoster.AddRange(this._segments);
        this._deadIndices.Clear();
    }

    // The latch is set only after the queue succeeds, so a throw cannot permanently suppress splits.
    private void QueueResolution()
    {
        if (this._resolutionQueued) { return; }

        Callable.From(this.ResolveSplits).CallDeferred();
        this._resolutionQueued = true;
    }

    private void ResolveSplits()
    {
        this._resolutionQueued = false;
        if (!this._resolutionOpen) { return; }

        var roster = this._resolutionRoster.ToArray();
        var dead = new HashSet<int>(this._deadIndices);
        var headDead = this._headDead;
        if (!this._promotionGroundRetryPending
            && this.RequiresPromotionGroundRetry(roster, dead, headDead))
        {
            this._promotionGroundRetryPending = true;
            this.GetTree().Connect(
                SceneTree.SignalName.PhysicsFrame,
                Callable.From(this.ResolveSplits),
                (uint)GodotObject.ConnectFlags.OneShot);
            return;
        }

        this._promotionGroundRetryPending = false;
        this._resolutionOpen = false;
        this._resolutionRoster.Clear();
        this._deadIndices.Clear();
        this._headDead = false;

        if (roster.Length == 0) { return; }

        // Captured before anything is freed: a dying head is already queued for deletion by now.
        var container = this._head != null && GodotObject.IsInstanceValid(this._head) ? this._head.GetParent() : null;
        var headRoot = this.Owner as Node3D ?? this._head;
        var parentSeed = 0;
        var parentSeedResolved = false;

        foreach (var range in SegmentSplitResolver.Resolve(dead, roster.Length))
        {
            var members = new List<BodySegment3D>(range.End.Value - range.Start.Value);
            for (var i = range.Start.Value; i < range.End.Value; i++)
            {
                if (GodotObject.IsInstanceValid(roster[i])) { members.Add(roster[i]); }
            }

            if (members.Count == 0) { continue; }

            // A living head keeps the run still attached to it, which is the one starting at its own end.
            if (!headDead && range.Start.Value == 0) { continue; }

            // Refusal targets the BURIED class only: a front unit whose column's walkable surface sits
            // ABOVE it is below the world and would promote into an out-of-bounds fall. A column with no
            // surface at all is a floorless world (bottomless fixture) — promote, matching the spawn
            // path's probe-window grace; the two cases emit the same under-probe miss and only the
            // elevated probe separates them. Probes borrow the HEAD's grounding contract (segment units
            // are not physics bodies); a contract-less head can never be buried.
            var frontPos = members[0].Body.GlobalPosition;
            var buried = this.IsFrontUnitBuried(frontPos);
            this.Detach(members);
            if (buried)
            {
                JmoLogger.Warning(this,
                    $"[SegmentedBody] refused promotion: the walkable surface over {frontPos} sits above the front unit — it is below the world, {members.Count} units");
                KillFragment(members, this);
                continue;
            }

            if (members.Count < this.MinLength)
            {
                JmoLogger.Info(this,
                    $"[SegmentedBody] severed fragment of {members.Count} unit(s) is under MinLength {this.MinLength} — killed, not promoted.");
                KillFragment(members, this);
                continue;
            }

            if (container == null || headRoot == null)
            {
                JmoLogger.Warning(this,
                    $"[SegmentedBody] a promotable {members.Count}-unit fragment was killed: the head left no container/root to promote beside.");
                KillFragment(members, this);
                continue;
            }

            if (!parentSeedResolved)
            {
                parentSeed = this.ResolveParentSeed();
                parentSeedResolved = true;
            }

            var progeny = ChainPromotion.Promote(this, headRoot, members, container, parentSeed, this._progenyOrdinal++);
            this.ProgenySpawned?.Invoke(progeny);
        }

        if (headDead || this.Length >= this.MinLength) { return; }

        this.BelowMinimumLength?.Invoke();
        this._headHealth?.Kill(this);
    }

    private bool RequiresPromotionGroundRetry(
        IReadOnlyList<BodySegment3D> roster,
        HashSet<int> dead,
        bool headDead)
    {
        if (this._head is not PhysicsBody3D || !GodotObject.IsInstanceValid(this._head)) { return false; }
        foreach (var range in SegmentSplitResolver.Resolve(dead, roster.Count))
        {
            if (!headDead && range.Start.Value == 0) { continue; }
            var front = roster[range.Start.Value];
            if (GodotObject.IsInstanceValid(front)
                && !this.TryProbeGround(front.Body.GlobalPosition, out _))
            {
                return true;
            }
        }

        return false;
    }

    private void Detach(IReadOnlyList<BodySegment3D> members)
    {
        foreach (var segment in members)
        {
            this._segments.Remove(segment);
            this.UnhookSegment(segment);
            if (!GodotObject.IsInstanceValid(segment)) { continue; }

            segment.Died -= this.OnSegmentDied;
            segment.Unbind();
        }

        this.Reindex();
    }

    private static void KillFragment(IReadOnlyList<BodySegment3D> members, object source)
    {
        foreach (var segment in members)
        {
            if (!GodotObject.IsInstanceValid(segment)) { continue; }
            if (segment.Health is HealthComponent health && !health.IsDead) { health.Kill(source); }
        }
    }

    private int ResolveParentSeed()
    {
        if (this.TryResolveHeadSeed(out var seed)) { return seed; }

        JmoLogger.Warning(this,
            "[Lineage] A segmented body promoted a fragment from an unseeded head, so the progeny derives its stream from zero.");
        return 0;
    }

    private void OnSegmentExiting(BodySegment3D segment)
        // A unit leaving the tree may be mid-reparent; only one still absent next idle frame is gone.
        => Callable.From(() => this.DropSegmentIfRemoved(segment)).CallDeferred();

    private void DropSegmentIfRemoved(BodySegment3D segment)
    {
        if (GodotObject.IsInstanceValid(segment) && segment.IsInsideTree()) { return; }

        this.DropSegment(segment);
    }

    private void DropSegment(BodySegment3D segment)
    {
        if (!this._segments.Remove(segment)) { return; }

        this.UnhookSegment(segment);
        if (GodotObject.IsInstanceValid(segment))
        {
            segment.Died -= this.OnSegmentDied;
            segment.Unbind();
        }

        this.Reindex();
    }

    private void Reindex()
    {
        for (var i = 0; i < this._segments.Count; i++)
        {
            this._segments[i].BindHost(this, i);
        }
    }

    private void ReseedFromCurrentPoses()
    {
        if (this._head == null || this._history == null) { return; }

        var poses = new List<(Vector3 position, Vector3 facing)>(this._segments.Count + 1)
        {
            (this._head.GlobalPosition, this._headFacing),
        };

        foreach (var segment in this._segments)
        {
            poses.Add((segment.Body.GlobalPosition, segment.Facing));
        }

        this._history.Reseed(poses);
    }

    private void FreeSegmentsIfRemoved()
    {
        if (GodotObject.IsInstanceValid(this) && this.IsInsideTree()) { return; }

        this.FreeSegments();
    }

    private void FreeSegments()
    {
        foreach (var segment in this._segments.ToArray())
        {
            if (!GodotObject.IsInstanceValid(segment)) { continue; }

            segment.Died -= this.OnSegmentDied;
            segment.Unbind();
            // The whole unit goes, not just its chain component.
            segment.Body.SafeQueueFree();
        }

        this._segments.Clear();
        this._exitHooks.Clear();
    }

    #region Test Helpers
#if TOOLS
    internal void _TestSetHead(Node3D head) => this._head = head;
    internal bool _TestIsFrontUnitBuried(Vector3 frontPos) => this.IsFrontUnitBuried(frontPos);
    internal void _TestSetSegmentRange(int initialMin, int initialMax, int minLength)
    {
        this.InitialSegmentsMin = initialMin;
        this.InitialSegmentsMax = initialMax;
        this.MinLength = minLength;
    }
#endif
    #endregion
}
