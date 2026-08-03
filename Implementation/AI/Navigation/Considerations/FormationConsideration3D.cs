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
[GlobalClass, Tool]
public partial class FormationConsideration3D : BaseAIConsideration3D
{
    #region Exported Parameters

    [ExportGroup("Formation Behavior")]

    /// <summary>
    /// If true, the member occupying slot 0 is not affected by this consideration — but only while the
    /// squad actually has a designated leader. Leaders drive the formation position rather than follow it;
    /// in a leaderless squad slot 0 is just the centre of the formation and its occupant must still steer.
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
    /// Distance at which the pull saturates: at or beyond this the aligned direction scores the full
    /// [0,1] scale, so Weight alone decides how loud this consideration is against its peers. Between
    /// the arrival radius and here the score ramps linearly from 0 to 1.
    /// </summary>
    [Export(PropertyHint.Range, "0.5, 20.0, 0.1")]
    private float _fullPullDistance = 4.0f;

    /// <summary>
    /// Hard cutoff: past this distance the agent is considered lost from formation and this
    /// consideration contributes nothing at all, leaving its steering entirely to its other
    /// considerations (chase, navigation, avoidance).
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
        IBlackboard blackboard,
        AIConsiderationRuntime? runtime)
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

        if (_excludeLeader && slotIndex == 0 && HasDesignatedLeader(blackboard, graph))
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

        // 7. Past the influence cutoff the member is lost from formation - contribute nothing.
        if (distanceToSlot > _maxInfluenceDistance)
        {
            return scores;
        }

        // 8. Calculate ideal direction
        Vector3 idealDirection = toSlot.Normalized();

        // 9. Band-relative [0,1] ramp: zero at the arrival radius, saturating at _fullPullDistance.
        // Normalising against the influence CEILING instead would fold an absolute distance scale into
        // the magnitude, leaving Weight only nominally in charge of cross-consideration loudness.
        float scoreBase = Mathf.Clamp(
            (distanceToSlot - _arrivalRadius) / Mathf.Max(Epsilon, _fullPullDistance - _arrivalRadius),
            0f, 1f);

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

    /// <summary>
    /// True only when the squad scope holds a live leader reference. A leader that was removed is
    /// written back as null, which the Blackboard stores as Variant.Nil and TryGet reports as a MISS —
    /// so both "never designated" and "designated then lost" correctly fall through to false here, and
    /// the leader exclusion cannot re-enable itself on a squad that no longer has a leader.
    /// </summary>
    private static bool HasDesignatedLeader(IBlackboard blackboard, IBlackboardGraph? graph)
    {
        if (graph != null)
        {
            return graph.TryGetUp<Node3D>(BBDataSig.FormationLeader, out var chainLeader) && chainLeader != null;
        }

        return blackboard.TryGet<Node3D>(BBDataSig.FormationLeader, out var localLeader) && localLeader != null;
    }
}
