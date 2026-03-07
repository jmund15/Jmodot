namespace Jmodot.Implementation.AI.Perception;

using System;
using Core.AI.Perception;
using Core.Identification;
using Shared;
using Strategies;

/// <summary>
/// Polling-based directional scanner using child RayCast3D nodes.
/// Fires percepts on state changes (new detection, lost contact) with distance-based confidence.
/// Redundant same-collider polls are suppressed to avoid event spam.
/// </summary>
[GlobalClass]
public partial class Raycast3DArraySensor3D : Node3D, IAISensor3D
{
    [Export] private MemoryDecayStrategy? _defaultDecayStrategy;
    [Export(PropertyHint.Range, "0.05,1.0,0.01")] private float _pollingInterval = 0.1f;

    [ExportGroup("Filtering")]
    [Export] private Godot.Collections.Array<Category> _categoryFilter = new();

    public event Action<IAISensor3D, Percept3D>? PerceptUpdated;

    public Node GetUnderlyingNode() => this;

    /// <summary>
    /// Calculates a confidence value based on distance from sensor origin.
    /// Returns 1.0 at zero distance, 0.0 at max distance, linearly interpolated.
    /// </summary>
    public static float CalculateConfidence(float distance, float maxDistance)
    {
        if (maxDistance <= 0f) { return 1.0f; }
        return Mathf.Clamp(1.0f - distance / maxDistance, 0f, 1f);
    }

    #region Test Helpers
#if TOOLS
    internal void SetDefaultDecayStrategy(MemoryDecayStrategy? strategy) => _defaultDecayStrategy = strategy;
    internal void SetCategoryFilter(Godot.Collections.Array<Category> filter) => _categoryFilter = filter;

    internal void SimulateRayStateChange(int rayIndex, GodotObject? previousCollider, GodotObject? currentCollider,
        Vector3 hitPosition, float rayLength)
    {
        ProcessRayStateChange(previousCollider, currentCollider, hitPosition, rayLength);
    }
#endif
    #endregion

    private void ProcessRayStateChange(GodotObject? previousCollider, GodotObject? currentCollider,
        Vector3 hitPosition, float rayLength)
    {
        if (_defaultDecayStrategy == null) { return; }

        // Same collider on consecutive polls — suppress redundant events
        if (previousCollider == currentCollider) { return; }

        // Target lost — fire confidence=0 percept for the previous collider
        if (currentCollider == null && previousCollider is Node3D prevBody)
        {
            FireLostPercept(prevBody);
            return;
        }

        // New detection or collider changed
        if (currentCollider is Node3D body)
        {
            FireDetectedPercept(body, hitPosition, rayLength);
        }
    }

    private void FireDetectedPercept(Node3D body, Vector3 hitPosition, float rayLength)
    {
        if (!TryResolveIdentity(body, out var identifiable)) { return; }

        var identity = identifiable!.GetIdentity();
        if (_categoryFilter.Count > 0 && !MatchesCategoryFilter(identity)) { return; }
        var strategy = identity.ResolvePerceptionDecay() ?? _defaultDecayStrategy!;
        float distance = GlobalPosition.DistanceTo(hitPosition);
        float confidence = CalculateConfidence(distance, rayLength);
        var velocity = ExtractVelocity(body);

        var percept = new Percept3D(
            target: body,
            position: hitPosition,
            velocity: velocity,
            identity: identity,
            confidence: confidence,
            decayStrategy: strategy
        );

        PerceptUpdated?.Invoke(this, percept);
    }

    private void FireLostPercept(Node3D body)
    {
        if (!TryResolveIdentity(body, out var identifiable)) { return; }

        var identity = identifiable!.GetIdentity();
        if (_categoryFilter.Count > 0 && !MatchesCategoryFilter(identity)) { return; }
        var strategy = identity.ResolvePerceptionDecay() ?? _defaultDecayStrategy!;

        var percept = new Percept3D(
            target: body,
            position: body.GlobalPosition,
            velocity: Vector3.Zero,
            identity: identity,
            confidence: 0.0f,
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
