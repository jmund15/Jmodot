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
/// When ContinuousTracking is enabled, tracked bodies are re-polled each physics frame
/// to update their positions. This is essential for flee/chase behavior where the target
/// moves within the sensor area — without it, the percept position is stale (set only on entry).
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

    private readonly HashSet<Node3D> _trackedBodies = new();

    public event Action<IAISensor3D, Percept3D>? PerceptUpdated;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
        _trackedBodies.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
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

    #region Test Helpers
#if TOOLS
    internal void SetDefaultDecayStrategy(MemoryDecayStrategy? strategy) => _defaultDecayStrategy = strategy;
    internal void SetCategoryFilter(Godot.Collections.Array<Category> filter) => _categoryFilter = filter;
    internal void SetContinuousTracking(bool enabled) => _continuousTracking = enabled;
    internal int TrackedBodyCount => _trackedBodies.Count;
    internal void SimulateBodyEntered(Node3D body) => OnBodyEntered(body);
    internal void SimulateBodyExited(Node3D body) => OnBodyExited(body);
    internal void SimulatePhysicsTick(double delta) => _PhysicsProcess(delta);
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
