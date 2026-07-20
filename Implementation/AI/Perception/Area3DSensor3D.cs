namespace Jmodot.Implementation.AI.Perception;

using System;
using System.Collections.Generic;
using AI.BB;
using Core.AI.BB;
using Core.AI.Perception;
using Core.Combat.EffectDefinitions;
using Core.Components;
using Core.Identification;
using Core.Stats;
using Shared;
using Strategies;

/// <summary>
/// Event-driven radius sensor that detects bodies entering/exiting an Area3D volume
/// and fires percepts into the AI perception pipeline.
///
/// Identity resolution uses the dual pattern: body is IIdentifiable → child IIdentifiable fallback.
/// Velocity extracted from CharacterBody3D; zero for static bodies.
///
/// <b>Detection Modes:</b>
/// <list type="bullet">
/// <item><b>Signal-driven (default):</b> Uses Godot's body_entered/body_exited signals.
/// Works for dynamic bodies (CharacterBody3D, RigidBody3D).</item>
/// <item><b>StaticBodyPolling:</b> Uses DirectSpaceState.IntersectShape each physics frame.
/// Required for detecting StaticBody3D because Godot's broadphase does not fire
/// body_entered for static bodies when the Area3D moves as a child of a CharacterBody3D.</item>
/// <item><b>ContinuousTracking:</b> Re-polls tracked bodies each physics frame to update positions.
/// Essential for flee/chase where the target moves within the sensor area.</item>
/// </list>
/// </summary>
[GlobalClass]
public partial class Area3DSensor3D : Area3D, IAISensor3D, IComponent
{
    [Export] private MemoryDecayStrategy? _defaultDecayStrategy;

    [ExportGroup("Filtering")]
    [Export] private Godot.Collections.Array<Category> _categoryFilter = new();

    [ExportGroup("Tracking")]
    /// <summary>
    /// When enabled, bodies inside the sensor area are re-polled each physics frame
    /// to update their position and velocity. Enable for moving targets (threats, players).
    /// Leave disabled for static targets (obstacles, environment) to avoid per-frame cost.
    /// </summary>
    [Export] private bool _continuousTracking;

    /// <summary>
    /// When enabled, uses DirectSpaceState.IntersectShape polling instead of body_entered signals.
    /// Required for detecting StaticBody3D nodes, which don't trigger body_entered on a moving Area3D.
    /// </summary>
    [Export] private bool _staticBodyPolling;

    /// <summary>
    /// How often (in seconds) to poll for static bodies. Lower = more responsive, higher = cheaper.
    /// Only used when StaticBodyPolling is enabled.
    /// </summary>
    [Export(PropertyHint.Range, "0.05, 1.0, 0.05")]
    private float _pollInterval = 0.25f;

    [ExportGroup("Line of Sight")]
    /// <summary>
    /// When enabled, overlap alone is not detection: a body only produces percepts while an
    /// occlusion ray from this sensor to it is clear. Blocked bodies stay tracked and re-check
    /// every physics frame, so a target stepping out of cover (or a wall being broken through)
    /// is picked up the moment the sightline opens; losing sight emits one confidence-0 percept,
    /// mirroring body-exit. Applies to the signal/tracking path only, not StaticBodyPolling
    /// (obstacle sensors sense the occluders themselves).
    /// </summary>
    [Export] private bool _requireLineOfSight;

    /// <summary>Physics layers that BLOCK sight (walls, closed doors). The ray excludes this
    /// sensor's parent body and the target, so layers shared with either are safe to include.</summary>
    [Export(PropertyHint.Layers3DPhysics)] private uint _occlusionMask = 3;

    /// <summary>Vertical offset applied to both ray endpoints — sight runs eye-to-eye, not
    /// feet-to-feet (a ground-level ray would clip floor geometry).</summary>
    [Export] private float _sightHeightOffset = 1.0f;

    /// <summary>Projects a resolved value onto the sensor's SphereShape3D radius, so the physical
    /// detection gate and any stat-reading selection gate are two readings of one authored number.
    /// Leave null to opt out — the authored radius then remains the gate. When an
    /// AttributeFloatDefinition is used, its DefaultValue should equal the authored radius, and the
    /// radius re-projects live whenever the backing stat changes (buffs, run modifiers).</summary>
    [ExportGroup("Range")]
    [Export] private BaseFloatValueDefinition? _rangeDefinition;

    private readonly HashSet<Node3D> _trackedBodies = new();
    private readonly HashSet<Node3D> _visibleBodies = new();
    private readonly HashSet<Node3D> _polledBodies = new();
    private float _pollTimer;
    private CollisionShape3D? _cachedCollisionShape;
    private IStatProvider? _statProvider;
    private bool _resyncPending;
    /// <summary>Every overlapping body, regardless of detection mode. Distinct from _trackedBodies,
    /// which stays empty unless ContinuousTracking or RequireLineOfSight is on — diffing a resize
    /// against that would re-announce every body on a plain sensor.</summary>
    private readonly HashSet<Node3D> _knownOverlaps = new();

    public event Action<IAISensor3D, Percept3D>? PerceptUpdated;

    #region IComponent Implementation

    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Always returns true. A sensor on a stat-less entity (spell body, practice dummy) is valid
    /// configuration, not a failure — returning false would make EntityNodeComponentsInitializer
    /// log an error and skip the component.
    /// </summary>
    public bool Initialize(IBlackboard bb)
    {
        if (_rangeDefinition == null)
        {
            IsInitialized = true;
            Initialized();
            OnPostInitialize();
            return true;
        }

        var shapeNode = ResolveCollisionShape();
        if (shapeNode?.Shape is SphereShape3D sphere)
        {
            // CombatSpawnHelper instantiates N enemies from one cached PackedScene, so Godot
            // hands every instance the SAME inline shape — writing Radius on one would write it
            // for all of them.
            shapeNode.Shape = (Shape3D)sphere.Duplicate(true);
        }

        bb.TryGet(BBDataSig.Stats, out _statProvider);

        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize()
    {
        if (_rangeDefinition == null) { return; }

        if (_statProvider != null)
        {
            _statProvider.OnStatChanged += OnStatProviderStatChanged;
        }

        ApplyRange(_rangeDefinition.ResolveFloatValue(_statProvider));
    }

    private void OnStatProviderStatChanged(Core.Stats.Attribute attribute, Variant newValue)
    {
        if (!IsInitialized || _rangeDefinition == null) { return; }
        ApplyRange(_rangeDefinition.ResolveFloatValue(_statProvider));
    }

    private void ApplyRange(float range)
    {
        var shapeNode = ResolveCollisionShape();
        if (shapeNode?.Shape is not SphereShape3D sphere) { return; }
        // OnStatChanged is coarse — it fires for every attribute, so most calls land here unchanged.
        if (Mathf.IsEqualApprox(sphere.Radius, range)) { return; }

        sphere.Radius = range;
        // Deferred to a physics frame, not CallDeferred: the physics server only reflects the
        // resized shape after it ticks, so an idle-frame resync reads stale overlaps.
        _resyncPending = true;
    }

    /// <summary>
    /// Replays the enter/exit delta after a resize. Whether Godot re-fires body_entered/body_exited
    /// for a resized shape is unverified, and this codebase does not trust engine overlap signals
    /// across state changes — the diff against _knownOverlaps makes this idempotent either way.
    /// </summary>
    private void ResyncOverlaps()
    {
        var current = new HashSet<Node3D>();
        foreach (var body in GetOverlappingBodies())
        {
            current.Add(body);
        }

        foreach (var body in new List<Node3D>(_knownOverlaps))
        {
            if (!current.Contains(body)) { OnBodyExited(body); }
        }

        foreach (var body in current)
        {
            if (!_knownOverlaps.Contains(body)) { OnBodyEntered(body); }
        }
    }

    /// <summary>
    /// By type, not by name — turret.tscn names this child "ThreatShape" and npc_template.tscn
    /// "AllySensorShape", both of which the old name-based lookup cached null. Absence is a
    /// supported shape (sensors may carry no shape at all), so this must not use the throwing
    /// GetFirstChildOfType variant.
    /// </summary>
    private CollisionShape3D? ResolveCollisionShape()
    {
        if (_cachedCollisionShape != null) { return _cachedCollisionShape; }
        this.TryGetFirstChildOfType(out CollisionShape3D? found);
        _cachedCollisionShape = found;
        return _cachedCollisionShape;
    }

    public event Action Initialized = delegate { };

    #endregion

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        // By type, not by name — turret.tscn names this child "ThreatShape" and
        // npc_template.tscn names it "AllySensorShape", both of which cached null.
        ResolveCollisionShape();
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
        if (_statProvider != null)
        {
            _statProvider.OnStatChanged -= OnStatProviderStatChanged;
        }
        _trackedBodies.Clear();
        _visibleBodies.Clear();
        _polledBodies.Clear();
        _knownOverlaps.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_resyncPending)
        {
            _resyncPending = false;
            ResyncOverlaps();
        }

        // Static body polling via IntersectShape (replaces body_entered for static bodies)
        if (_staticBodyPolling)
        {
            _pollTimer += (float)delta;
            if (_pollTimer >= _pollInterval)
            {
                _pollTimer = 0f;
                PollStaticBodies();
            }
        }

        if ((!_continuousTracking && !_requireLineOfSight) || _trackedBodies.Count == 0) { return; }

        // Re-poll all tracked bodies to update their positions
        // Use a snapshot to avoid collection modification during iteration
        var snapshot = new List<Node3D>(_trackedBodies);
        foreach (var body in snapshot)
        {
            if (!body.IsValid())
            {
                _trackedBodies.Remove(body);
                _visibleBodies.Remove(body);
                continue;
            }
            if (_requireLineOfSight)
            {
                ProcessSighted(body);
            }
            else
            {
                ProcessDetection(body, 1.0f);
            }
        }
    }

    // LOS-gated per-frame detection: emit while visible; on visible→blocked emit one
    // confidence-0 percept (mirrors body-exit); while blocked stay silent.
    private void ProcessSighted(Node3D body)
    {
        if (HasLineOfSight(body))
        {
            _visibleBodies.Add(body);
            ProcessDetection(body, 1.0f);
        }
        else if (_visibleBodies.Remove(body))
        {
            ProcessDetection(body, 0.0f);
        }
    }

    private bool HasLineOfSight(Node3D body)
    {
        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) { return true; }

        var offset = Vector3.Up * _sightHeightOffset;
        var query = PhysicsRayQueryParameters3D.Create(
            GlobalPosition + offset, body.GlobalPosition + offset, _occlusionMask);
        var exclude = new Godot.Collections.Array<Rid>();
        if (GetParent() is CollisionObject3D self) { exclude.Add(self.GetRid()); }
        if (body is CollisionObject3D target) { exclude.Add(target.GetRid()); }
        query.Exclude = exclude;
        return spaceState.IntersectRay(query).Count == 0;
    }

    public Node GetUnderlyingNode() => this;

    #region Static Body Polling

    /// <summary>
    /// Polls for overlapping bodies using DirectSpaceState.IntersectShape.
    /// This bypasses Godot's broken Area3D overlap tracking for StaticBody3D.
    /// Builds the current body set via physics query then delegates to ApplyPollResults.
    /// </summary>
    private void PollStaticBodies()
    {
        if (_cachedCollisionShape?.Shape == null) { return; }

        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) { return; }

        var query = new PhysicsShapeQueryParameters3D();
        query.Shape = _cachedCollisionShape.Shape;
        query.Transform = GlobalTransform;
        query.CollisionMask = CollisionMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var results = spaceState.IntersectShape(query, 32);

        var currentBodies = new HashSet<Node3D>();
        foreach (var result in results)
        {
            if (result.TryGetValue("collider", out var collider) && collider.Obj is Node3D body)
            {
                if (body == GetParent()) { continue; }
                currentBodies.Add(body);
            }
        }

        ApplyPollResults(currentBodies);
    }

    /// <summary>
    /// Applies a poll result set: fires percepts for all detected bodies and exits for removed ones.
    /// All currently-detected bodies are refreshed every cycle so static obstacle memories
    /// never expire from ForgetTime-based decay while walls remain within sensor range.
    /// </summary>
    private void ApplyPollResults(HashSet<Node3D> currentBodies)
    {
        // Refresh ALL detected bodies — not just new ones.
        // Static walls are always within range, so "new entries only" means the percept fires
        // once and then the memory decays after ForgetTime with no refresh.
        foreach (var body in currentBodies)
        {
            ProcessDetection(body, 1.0f);
        }

        // Exits: in previous poll but absent from current
        foreach (var body in _polledBodies)
        {
            if (!currentBodies.Contains(body) && body.IsValid())
            {
                ProcessDetection(body, 0.0f);
            }
        }

        _polledBodies.Clear();
        foreach (var body in currentBodies) { _polledBodies.Add(body); }
    }

    #endregion

    #region Test Helpers
#if TOOLS
    internal void SetDefaultDecayStrategy(MemoryDecayStrategy? strategy) => _defaultDecayStrategy = strategy;
    internal void SetCategoryFilter(Godot.Collections.Array<Category> filter) => _categoryFilter = filter;
    internal void SetContinuousTracking(bool enabled) => _continuousTracking = enabled;
    internal void SetStaticBodyPolling(bool enabled) => _staticBodyPolling = enabled;
    internal void SetRequireLineOfSight(bool enabled) => _requireLineOfSight = enabled;
    internal void SetOcclusionMask(uint mask) => _occlusionMask = mask;
    internal void SetRangeDefinitionForTesting(BaseFloatValueDefinition? definition) => _rangeDefinition = definition;
    internal int VisibleBodyCount => _visibleBodies.Count;
    internal int TrackedBodyCount => _trackedBodies.Count;
    internal int PolledBodyCount => _polledBodies.Count;
    internal void SimulateBodyEntered(Node3D body) => OnBodyEntered(body);
    internal void SimulateBodyExited(Node3D body) => OnBodyExited(body);
    internal void SimulatePhysicsTick(double delta) => _PhysicsProcess(delta);
    /// <summary>
    /// Directly exercises ApplyPollResults without a physics query.
    /// Use to test static body polling refresh behavior in unit tests.
    /// </summary>
    internal void SimulatePolledBodies(IEnumerable<Node3D> bodies)
        => ApplyPollResults(new HashSet<Node3D>(bodies));
#endif
    #endregion

    private void OnBodyEntered(Node3D body)
    {
        _knownOverlaps.Add(body);
        if (_continuousTracking || _requireLineOfSight) { _trackedBodies.Add(body); }
        if (_requireLineOfSight)
        {
            // No immediate emit: the space state may be locked during signal flush, and the body
            // may be occluded anyway. The next _PhysicsProcess pass announces it if sighted.
            return;
        }
        ProcessDetection(body, 1.0f);
    }

    private void OnBodyExited(Node3D body)
    {
        _knownOverlaps.Remove(body);
        _trackedBodies.Remove(body);
        if (_requireLineOfSight)
        {
            // Only announce the exit of a body that was ever announced as sighted.
            if (_visibleBodies.Remove(body)) { ProcessDetection(body, 0.0f); }
            return;
        }
        ProcessDetection(body, 0.0f);
    }

    private void ProcessDetection(Node3D body, float confidence)
    {
        if (_defaultDecayStrategy == null) { return; }
        if (!TryResolveIdentity(body, out var identifiable)) { return; }

        var identity = identifiable!.GetIdentity();
        if (_categoryFilter.Count > 0 && !MatchesCategoryFilter(identity)) { return; }
        var strategy = identity.ResolvePerceptionDecay() ?? _defaultDecayStrategy;
        var velocity = ExtractVelocity(body);
        var position = body.GlobalPosition;

        var percept = new Percept3D(
            target: body,
            position: position,
            velocity: velocity,
            identity: identity,
            confidence: confidence,
            decayStrategy: strategy
        );

        PerceptUpdated?.Invoke(this, percept);
    }

    private bool MatchesCategoryFilter(Identity identity)
    {
        foreach (var filterCat in _categoryFilter)
        {
            if (identity.HasCategory(filterCat)) { return true; }
        }
        return false;
    }

    private static bool TryResolveIdentity(Node3D body, out IIdentifiable? identifiable)
    {
        if (body is IIdentifiable directId)
        {
            identifiable = directId;
            return true;
        }

        return body.TryGetFirstChildOfInterface(out identifiable);
    }

    private static Vector3 ExtractVelocity(Node3D body)
    {
        if (body is CharacterBody3D cb) { return cb.Velocity; }
        if (body is RigidBody3D rb) { return rb.LinearVelocity; }
        return Vector3.Zero;
    }
}
