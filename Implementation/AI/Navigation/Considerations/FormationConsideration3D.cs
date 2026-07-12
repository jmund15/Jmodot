namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using BB;

/// <summary>
/// A steering consideration that guides agents toward their assigned formation slot.
/// Members read their slot index from their own blackboard and slot positions from the
/// squad blackboard (via parent chain). The leader (slot 0) can be optionally excluded.
/// </summary>
[GlobalClass]
public partial class FormationConsideration3D : BaseAIConsideration3D
{
    #region Exported Parameters

    [ExportGroup("Formation Behavior")]

    /// <summary>
    /// If true, members in slot 0 (the leader) will not be affected by this consideration.
    /// Leaders typically drive the formation position, not follow it.
    /// </summary>
    [Export]
    private bool _excludeLeader = true;

    /// <summary>
    /// Distance at which the agent is considered "arrived" at the slot.
    /// Within this radius, no steering force is applied.
    /// </summary>
    [Export(PropertyHint.Range, "0.5, 5.0, 0.1")]
    private float _arrivalRadius = 1.5f;

    /// <summary>
    /// Maximum distance at which the formation still exerts influence.
    /// Beyond this, the agent may be considered "lost" from formation.
    /// </summary>
    [Export(PropertyHint.Range, "5.0, 50.0, 1.0")]
    private float _maxInfluenceDistance = 20.0f;

    #endregion

    private const float Epsilon = 0.001f;

    /// <summary>
    /// Calculates scores for each direction based on the agent's assigned formation slot.
    /// </summary>
    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard)
    {
        // Initialize all directions with zero scores
        var scores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        // Cross-scope reads (FormationActive, FormationSlotPositions) live on the squad scope;
        // FormationSlotIndex is agent-local. The graph carries the cross-scope walk; local-only
        // fallback preserves test fixtures that haven't wired a graph hierarchy.
        var graph = blackboard.FindParentGraph();

        bool isActive;
        if (graph != null)
        {
            if (!graph.TryGetUp<bool>(BBDataSig.FormationActive, out isActive) || !isActive) { return scores; }
        }
        else
        {
            if (!blackboard.TryGet<bool>(BBDataSig.FormationActive, out isActive) || !isActive) { return scores; }
        }

        if (!blackboard.TryGet<int>(BBDataSig.FormationSlotIndex, out var slotIndex) || slotIndex < 0)
        {
            return scores;
        }

        if (_excludeLeader && slotIndex == 0)
        {
            return scores;
        }

        Dictionary<int, Vector3>? slotPositions;
        if (graph != null)
        {
            if (!graph.TryGetUp<Dictionary<int, Vector3>>(BBDataSig.FormationSlotPositions, out slotPositions)) { return scores; }
        }
        else
        {
            if (!blackboard.TryGet<Dictionary<int, Vector3>>(BBDataSig.FormationSlotPositions, out slotPositions)) { return scores; }
        }
        if (slotPositions == null || !slotPositions.TryGetValue(slotIndex, out var targetSlotPosition))
        {
            return scores;
        }

        // 5. Calculate direction to assigned slot
        Vector3 agentPosition = context3D.AgentPosition;
        Vector3 toSlot = targetSlotPosition - agentPosition;

        // Flatten for ground-based movement
        toSlot.Y = 0;

        float distanceToSlot = toSlot.Length();

        // 6. Check arrival - no steering needed if we're close enough
        if (distanceToSlot < _arrivalRadius)
        {
            return scores;
        }

        // 7. Check max influence - don't steer if too far (optional clamping)
        if (distanceToSlot > _maxInfluenceDistance)
        {
            distanceToSlot = _maxInfluenceDistance;
        }

        // 8. Calculate ideal direction
        Vector3 idealDirection = toSlot.Normalized();

        // 9. Calculate normalized [0,1] score magnitude based on distance (base Weight owns
        // the cross-consideration magnitude). Further from slot = higher urgency (stronger pull).
        float distanceFactor = distanceToSlot / _maxInfluenceDistance;
        float scoreBase = distanceFactor;

        // 10. Score each direction based on alignment with ideal direction
        foreach (var availableDir in directions.Directions)
        {
            Vector3 flatDir = availableDir;
            flatDir.Y = 0;

            if (flatDir.LengthSquared() < Epsilon)
            {
                continue;
            }

            flatDir = flatDir.Normalized();
            float alignment = flatDir.Dot(idealDirection);

            // Only score directions that point toward the slot (positive alignment)
            if (alignment > 0)
            {
                scores[availableDir] = scoreBase * alignment;
            }
        }

        return scores;
    }
}
