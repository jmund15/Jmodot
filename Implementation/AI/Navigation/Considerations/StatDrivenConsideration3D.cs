namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Core.Stats;
using BB;
using Shared;

/// <summary>
/// A steering consideration that uses stat values to parameterize its behavior.
/// The primary use case is using a SightRange stat to determine how far the agent
/// can perceive targets, making AI perception stat-driven and modifiable by buffs/debuffs.
/// </summary>
[GlobalClass]
public partial class StatDrivenConsideration3D : BaseAIConsideration3D
{
    #region Exported Parameters

    [ExportGroup("Stat Configuration")]

    /// <summary>
    /// The attribute that determines the consideration's influence range.
    /// If not set or stats unavailable, falls back to DefaultInfluenceRange.
    /// </summary>
    [Export]
    public Attribute? InfluenceRangeAttribute { get; set; }

    /// <summary>
    /// Fallback value for influence range when stat is unavailable.
    /// </summary>
    [Export(PropertyHint.Range, "1.0, 100.0, 0.5")]
    public float DefaultInfluenceRange { get; set; } = 15.0f;

    [ExportGroup("Steering Behavior")]

    /// <summary>
    /// Weight multiplier for the steering scores.
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
    private float _weight = 1.0f;

    /// <summary>
    /// Distance at which the agent is considered "arrived" at target.
    /// Within this radius, no steering force is applied.
    /// </summary>
    [Export(PropertyHint.Range, "0.5, 5.0, 0.1")]
    private float _arrivalRadius = 1.5f;

    [ExportGroup("Score Propagation")]

    /// <summary>
    /// If enabled, scores propagate to neighboring directions for smoother steering.
    /// </summary>
    [Export]
    private bool _propagateScores = true;

    /// <summary>
    /// Number of neighboring directions on each side to propagate scores to.
    /// </summary>
    [Export(PropertyHint.Range, "1, 4, 1")]
    private int _dirsToPropagate = 2;

    /// <summary>
    /// Multiplier applied to score for each propagation step.
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 0.9, 0.05")]
    private float _propDiminishWeight = 0.5f;

    #endregion

    private List<Vector3> _orderedDirections = null!;
    private const float Epsilon = 0.001f;

    /// <summary>
    /// Caches the ordered direction list for propagation.
    /// </summary>
    public override void Initialize(DirectionSet3D directions)
    {
        _orderedDirections = directions.Directions.ToList();
    }

    /// <summary>
    /// Calculates steering scores toward the current target, using stat values
    /// to determine the effective influence range.
    /// </summary>
    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard)
    {
        // Initialize all directions with zero scores
        var scores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        // 1. Get target position from blackboard
        if (!blackboard.TryGet<Vector3>(BBDataSig.CurrentTarget, out var targetPosition))
        {
            return scores;
        }

        // 2. Get the effective influence range (from stats or default)
        float influenceRange = GetInfluenceRange(blackboard);

        // 3. Calculate direction to target
        Vector3 agentPosition = context3D.AgentPosition;
        Vector3 toTarget = targetPosition - agentPosition;

        // Flatten for ground-based movement
        toTarget.Y = 0;

        float distanceToTarget = toTarget.Length();

        // 4. Check if within arrival radius (no steering needed)
        if (distanceToTarget < _arrivalRadius)
        {
            return scores;
        }

        // 5. Check if target is outside influence range
        if (distanceToTarget > influenceRange)
        {
            return scores;
        }

        // 6. Calculate ideal direction
        Vector3 idealDirection = toTarget.Normalized();

        // 7. Calculate score magnitude based on distance
        // Closer to target = lower urgency (softer approach)
        float distanceFactor = Mathf.Clamp(distanceToTarget / influenceRange, 0f, 1f);
        float scoreBase = _weight * distanceFactor;

        // 8. Score each direction based on alignment with ideal direction
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

            // Only score directions that point toward the target (positive alignment)
            if (alignment > 0)
            {
                scores[availableDir] = scoreBase * alignment;
            }
        }

        // 9. Apply score propagation if enabled
        if (_propagateScores)
        {
            Propagate(ref scores);
        }

        return scores;
    }

    /// <summary>
    /// Gets the effective influence range, reading from stats if available.
    /// </summary>
    private float GetInfluenceRange(IBlackboard blackboard)
    {
        // If no attribute configured, use default
        if (InfluenceRangeAttribute == null)
        {
            return DefaultInfluenceRange;
        }

        // Try to get stat provider
        if (!blackboard.TryGet<IStatProvider>(BBDataSig.Stats, out var stats) || stats == null)
        {
            return DefaultInfluenceRange;
        }

        // Get stat value, fallback to default if not found
        return stats.GetStatValue<float>(InfluenceRangeAttribute, DefaultInfluenceRange);
    }

    /// <summary>
    /// Propagates scores to neighboring directions for smoother steering.
    /// </summary>
    private void Propagate(ref Dictionary<Vector3, float> scores)
    {
        if (_orderedDirections == null || _orderedDirections.Count == 0)
        {
            return;
        }

        var initialScores = new Dictionary<Vector3, float>(scores);
        int dirCount = _orderedDirections.Count;

        for (int i = 0; i < dirCount; i++)
        {
            float initialScore = initialScores[_orderedDirections[i]];
            if (initialScore <= 0f)
            {
                continue;
            }

            float propWeight = initialScore * _propDiminishWeight;

            // Propagate to left and right neighbors
            for (int j = 1; j <= _dirsToPropagate; j++)
            {
                // Left neighbor with wrap-around
                int leftIndex = (i - j + dirCount) % dirCount;
                scores[_orderedDirections[leftIndex]] += propWeight;

                // Right neighbor with wrap-around
                int rightIndex = (i + j) % dirCount;
                scores[_orderedDirections[rightIndex]] += propWeight;

                // Diminish weight for next set of neighbors
                propWeight *= _propDiminishWeight;
            }
        }
    }
}
