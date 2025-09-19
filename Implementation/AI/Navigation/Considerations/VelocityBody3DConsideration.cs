namespace Jmodot.Implementation.AI.Navigation.Considerations;

using Godot;
using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.AI.Perception;
using Core.Identification;
using Core.Movement;
using Shared;

/// <summary>
/// A steering consideration that evaluates dynamic targets with velocities.
/// It calculates scores for chasing or avoiding targets by blending two core strategies:
/// 1. Position-based: Moving directly towards or away from the target's current location.
/// 2. Velocity-based: Aligning with or moving opposite to the target's velocity.
/// This allows for sophisticated behaviors like intercepting a moving target (chase) or
/// dodging an incoming projectile (avoid).
/// </summary>
[GlobalClass]
public partial class VelocityBody3DConsideration : BaseAIConsideration3D
{
    #region EXPORTED_PARAMETERS
    [ExportGroup("Behavior Tuning")]

    /// <summary>
    /// The overall weight and mode of this consideration.
    /// Positive values (> 0) activate "Chase" mode.
    /// Negative values (< 0) activate "Avoid" mode.
    /// The magnitude determines the strength of the interest or danger.
    /// </summary>
    [Export(PropertyHint.Range, "-5.0, 5.0, 0.1")]
    private float _considerationWeight = -1.0f; // Default to avoidance

    /// <summary>
    /// The Category resource used to filter which perceived targets this consideration will react to.
    /// For example, you could assign an "Enemy" Category to react to hostile units.
    /// </summary>
    [Export] private Category _targetCategory = null!;

    /// <summary>
    /// Balances the agent's strategy between position and velocity.
    /// Range: -1.0 to 1.0.
    /// For Avoidance: 1.0 = Flee from position; -1.0 = Dodge incoming velocity.
    /// For Chasing:   1.0 = Direct pursuit;   -1.0 = Match velocity (for interception).
    /// A value of 0.0 creates a balanced mix.
    /// </summary>
    [Export(PropertyHint.Range, "-1.0, 1.0, 0.1")]
    private float _positionVelocityBalance = 0.4f;

    /// <summary>
    /// If true, calculations will include the Y-axis. If false, all vectors will be flattened
    /// onto the XZ plane for ground-based agents.
    /// </summary>
    [Export] private bool _hasVerticalMovement = false;

    [ExportGroup("Score Propagation")]

    /// <summary>
    /// If enabled, the calculated score for an ideal direction will be "bled" to its
    /// neighboring directions, creating a smoother, less jerky response.
    /// </summary>
    [Export] private bool _propagateScores = true;

    /// <summary>
    /// The number of neighboring directions on each side to propagate the score to.
    /// </summary>
    [Export(PropertyHint.Range, "1, 8, 1")]
    private int _dirsToPropagate = 2;

    /// <summary>
    /// The multiplier applied to the score for each step of propagation. A value of 0.5
    /// means the first neighbor gets 50% of the original score, the second gets 25%, etc.
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 0.9, 0.05")]
    private float _propDiminishWeight = 0.5f;
    #endregion

    private const float Epsilon = 0.001f;
    private List<Vector3> _orderedDirections = null!; // Cached for propagation logic

    /// <summary>
    /// Caches the ordered list of directions from the DirectionSet3D. This is crucial
    /// for the score propagation logic to find a direction's neighbors reliably.
    /// </summary>
    public override void Initialize(DirectionSet3D directions)
    {
        _orderedDirections = directions.Directions.ToList();

        if (_targetCategory == null)
        {
            JmoLogger.Error(this, $"No '_targetCategory' assigned in {ResourcePath}. Consideration will have no effect.");
        }
    }

    /// <summary>
    /// The main evaluation method called by the AISteeringProcessor.
    /// It calculates scores by identifying relevant targets from the agent's memory and
    /// aggregating the behavioral scores for each one.
    /// </summary>
    protected override Dictionary<Vector3, float> CalculateBaseScores(DirectionSet3D directions, SteeringDecisionContext context, IBlackboard blackboard)
    {
        var finalScores = directions.Directions.ToDictionary(dir => dir, dir => 0f);

        // Get all relevant targets from the agent's perception manager.
        var relevantPercepts = context.Memory.GetSensedByCategory(_targetCategory);

        // Calculate and aggregate the scores for each perceived target.
        foreach (var perceptInfo in relevantPercepts)
        {
            var targetScores = GetScoresForSingleTarget(perceptInfo, directions, context);
            foreach (var score in targetScores)
            {
                finalScores[score.Key] += score.Value;
            }
        }

        if (_propagateScores)
        {
            Propagate(ref finalScores);
        }

        return finalScores;
    }

    /// <summary>
    /// Calculates the directional scores for a single dynamic target.
    /// </summary>
    private Dictionary<Vector3, float> GetScoresForSingleTarget(PerceptionInfo targetInfo, DirectionSet3D directions, SteeringDecisionContext context)
    {
        var considerations = directions.Directions.ToDictionary(dir => dir, dir => 0f);
        if (Mathf.Abs(_considerationWeight) < Epsilon) { return considerations; }

        // --- 1. Gather Data from Context and Percept ---
        Vector3 targetPosition = targetInfo.LastKnownPosition;
        Vector3 targetVelocity = targetInfo.LastKnownVelocity;
        Vector3 agentPosition = context.AgentPosition;
        Vector3 agentVelocity = context.AgentVelocity;

        // --- 2. Calculate Relative Vectors ---
        Vector3 toTargetVector = targetPosition - agentPosition;
        Vector3 relativeVelocity = targetVelocity - agentVelocity;

        // Flatten vectors if the agent is restricted to ground movement.
        if (!_hasVerticalMovement)
        {
            toTargetVector.Y = 0;
            relativeVelocity.Y = 0;
        }

        // --- 3. Normalize and Validate Vectors ---
        Vector3 toTargetDir = toTargetVector.LengthSquared() > Epsilon * Epsilon ? toTargetVector.Normalized() : Vector3.Zero;
        Vector3 relativeVelDir = relativeVelocity.LengthSquared() > Epsilon * Epsilon ? relativeVelocity.Normalized() : Vector3.Zero;
        bool useVelocity = relativeVelDir.LengthSquared() > Epsilon;

        // If the target is right on top of us and there's no relative velocity, there's nothing to consider.
        if (toTargetDir.IsZeroApprox() && !useVelocity) return considerations;

        // --- 4. Determine the Ideal Movement Direction based on Mode (Chase/Avoid) ---
        Vector3 idealDirection;
        bool shouldChase = _considerationWeight > 0;

        if (shouldChase)
        {
            idealDirection = CalculateChaseDirection(targetPosition, targetVelocity, toTargetDir, relativeVelDir, useVelocity);
        }
        else // Avoid
        {
            idealDirection = CalculateAvoidDirection(toTargetDir, relativeVelDir, useVelocity);
        }

        if (idealDirection.IsZeroApprox()) return considerations;

        // --- 5. Calculate Score Magnitude ---
        // The base score is influenced by the weight and decreases with distance to the target.
        float distance = Mathf.Max(1.0f, toTargetVector.Length()); // Avoid division by zero
        float weightFactor = Mathf.Abs(_considerationWeight) / distance;

        // --- 6. Distribute Scores to Available Directions ---
        // The final score for each direction is based on how well it aligns with the "ideal" direction.
        foreach (var availableDir in directions.Directions)
        {
            Vector3 finalAvailableDir = availableDir;
            if (!_hasVerticalMovement)
            {
                finalAvailableDir.Y = 0;
                if (finalAvailableDir.IsZeroApprox()) continue;
                finalAvailableDir = finalAvailableDir.Normalized();
            }

            float alignment = finalAvailableDir.Dot(idealDirection);
            float scoreMultiplier = Mathf.Max(0f, alignment); // Only consider positive alignment
            considerations[availableDir] = weightFactor * scoreMultiplier;
        }

        return considerations;
    }

    #region CORE_BEHAVIOR_LOGIC
    /// <summary>
    /// Calculates the ideal direction for chasing a target, blending direct pursuit with velocity matching for interception.
    /// </summary>
    private Vector3 CalculateChaseDirection(Vector3 targetPosition, Vector3 targetVelocity, Vector3 toTargetDir, Vector3 relativeVelDir, bool useVelocity)
    {
        Vector3 positionBasedDir = toTargetDir.IsZeroApprox() ? Vector3.Forward : toTargetDir;
        Vector3 velocityBasedDir;

        if (useVelocity && targetVelocity.LengthSquared() > Epsilon)
        {
            // To intercept, we want to move parallel to the target's absolute velocity.
            velocityBasedDir = targetVelocity.Normalized();
        }
        else
        {
            // If the target isn't moving, fall back to a pure position-based chase.
            return positionBasedDir;
        }

        // Blend the two strategies based on the balance parameter.
        // A negative balance favors velocity matching (interception).
        float interceptWeight = (-_positionVelocityBalance + 1.0f) / 2.0f;
        float positionWeight = 1.0f - interceptWeight;
        Vector3 blendedDirection = (positionBasedDir * positionWeight + velocityBasedDir * interceptWeight);

        // If blending cancels the vectors out, prioritize the positional chase.
        if (blendedDirection.LengthSquared() < Epsilon)
        {
            return positionBasedDir;
        }

        return blendedDirection.Normalized();
    }

    /// <summary>
    /// Calculates the ideal direction for avoiding a target, blending fleeing from its position with dodging its velocity.
    /// </summary>
    private Vector3 CalculateAvoidDirection(Vector3 toTargetDir, Vector3 relativeVelDir, bool useVelocity)
    {
        // The "flee" direction is directly away from the target's position.
        Vector3 positionBasedDir = toTargetDir.IsZeroApprox() ? Vector3.Back : -toTargetDir;

        if (!useVelocity)
        {
            return positionBasedDir;
        }

        // The "dodge" direction is directly opposite the target's relative velocity towards us.
        Vector3 velocityBasedDir = -relativeVelDir;

        // Blend the two strategies. A negative balance favors velocity-based dodging.
        float velocityWeight = (-_positionVelocityBalance + 1.0f) / 2.0f;
        float positionWeight = 1.0f - velocityWeight;
        Vector3 blendedDirection = (positionBasedDir * positionWeight + velocityBasedDir * velocityWeight);

        // If blending cancels the vectors out, prioritize fleeing from the position.
        if (blendedDirection.LengthSquared() < Epsilon)
        {
            return positionBasedDir;
        }

        return blendedDirection.Normalized();
    }

    /// <summary>
    /// Propagates scores to neighboring directions to create a smoother AI response.
    /// </summary>
    private void Propagate(ref Dictionary<Vector3, float> scores)
    {
        if (_orderedDirections == null || _orderedDirections.Count == 0) return;

        var initialScores = new Dictionary<Vector3, float>(scores);
        int dirCount = _orderedDirections.Count;

        for (int i = 0; i < dirCount; i++)
        {
            float initialScore = initialScores[_orderedDirections[i]];
            if (initialScore <= 0f) continue;

            float propWeight = initialScore * _propDiminishWeight;

            // Propagate to the left and right neighbors.
            for (int j = 1; j <= _dirsToPropagate; j++)
            {
                // Left neighbor with wrap-around
                int leftIndex = (i - j + dirCount) % dirCount;
                scores[_orderedDirections[leftIndex]] += propWeight;

                // Right neighbor with wrap-around
                int rightIndex = (i + j) % dirCount;
                scores[_orderedDirections[rightIndex]] += propWeight;

                // Diminish the weight for the next set of neighbors
                propWeight *= _propDiminishWeight;
            }
        }
    }
    #endregion
}
