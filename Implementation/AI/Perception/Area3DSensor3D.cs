namespace Jmodot.Implementation.AI.Perception;

using System;
using System.Collections.Generic;
using Core.AI.Perception;
using Core.Identification;
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
public partial class Area3DSensor3D : Area3D, IAISensor3D
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

    private readonly HashSet<Node3D> _trackedBodies = new();
    private readonly HashSet<Node3D> _polledBodies = new();
    private float _pollTimer;
    private CollisionShape3D? _cachedCollisionShape;

    public event Action<IAISensor3D, Percept3D>? PerceptUpdated;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        // Cache the collision shape for IntersectShape polling
        _cachedCollisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
        _trackedBodies.Clear();
        _polledBodies.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
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

        if (!_continuousTracking || _trackedBodies.Count == 0) { return; }

        // Re-poll all tracked bodies to update their positions
        // Use a snapshot to avoid collection modification during iteration
        var snapshot = new List<Node3D>(_trackedBodies);
        foreach (var body in snapshot)
        {
            if (!body.IsValid())
            {
                _trackedBodies.Remove(body);
                continue;
            }
            ProcessDetection(body, 1.0f);
        }
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
        if (_continuousTracking) { _trackedBodies.Add(body); }
        ProcessDetection(body, 1.0f);
    }

    private void OnBodyExited(Node3D body)
    {
        _trackedBodies.Remove(body);
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
