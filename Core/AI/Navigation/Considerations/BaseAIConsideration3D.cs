namespace Jmodot.Core.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using BB;
using Implementation.AI.Navigation;
using Implementation.AI.Navigation.Considerations;
using Movement;
using SteeringModifiers;
using GColl = Godot.Collections;

/// <summary>
/// The abstract base class for any environmental consideration. Its purpose is to
/// evaluate the current world state (via the SteeringDecisionContext) and produce a
/// dictionary of scores, where each key is a direction and each value is a float
/// representing the "interest" or "danger" associated with moving in that direction.
///
/// Score propagation (neighbor bleed-over) is handled centrally here via the optional
/// SteeringPropagationConfig export. Derived classes only need to implement
/// CalculateBaseScores — propagation is applied automatically in Evaluate().
/// </summary>
[GlobalClass, Tool]
public abstract partial class BaseAIConsideration3D : Resource
{
    /// <summary>
    /// The evaluation priority of this consideration. While all considerations are summed
    /// together, this can be used for debugging or for systems that may need a specific order.
    /// </summary>
    [Export] public int Priority { get; protected set; } = 1;

    /// <summary>
    /// A list of modifiers that can alter the objective scores of this consideration,
    /// allowing an AI's personality (Affinities) to influence its low-level behavior.
    /// </summary>
    [Export] private GColl.Array<SteeringConsiderationModifier3D> _modifiers = new();

    /// <summary>
    /// Optional propagation config that smooths scores by bleeding them to neighboring
    /// directions. Default config: NeighborCount=2, DiminishWeight=0.5.
    /// Set to null to disable propagation entirely.
    /// </summary>
    [ExportGroup("Score Propagation")] [Export]
    private SteeringPropagationConfig? _propagation;

    /// <summary>
    /// Cached ordered direction list for propagation. Populated in Initialize().
    /// </summary>
    private List<Vector3>? _cachedOrderedDirections;

    /// <summary>
    /// Called once by the AISteeringProcessor during initialization. This allows the
    /// consideration to perform any necessary setup or caching. Derived classes that
    /// override this MUST call base.Initialize(directions) for propagation to work.
    /// </summary>
    /// <param name="directions">The DirectionSet3D used by the agent.</param>
    public virtual void Initialize(DirectionSet3D directions)
    {
        _cachedOrderedDirections = directions.Directions.ToList();
    }

    /// <summary>
    /// The primary evaluation method. Calculates base scores, applies propagation,
    /// then applies subjective modifiers before adding to the final scores.
    /// </summary>
    public void Evaluate(SteeringDecisionContext3D context3D, IBlackboard blackboard,
        DirectionSet3D directions, ref Dictionary<Vector3, float> finalScores)
    {
        // 1. Calculate the raw, objective scores for this consideration.
        var baseScores = CalculateBaseScores(directions, context3D, blackboard);

        // 2. Apply propagation smoothing (neighbor bleed-over).
        if (_propagation != null && _cachedOrderedDirections != null)
        {
            SteeringPropagation.PropagateScores(
                baseScores, _cachedOrderedDirections,
                _propagation.NeighborCount, _propagation.DiminishWeight,
                _propagation.PropagateNegative);
        }

        // 3. Apply all subjective modifiers to the scores.
        foreach (var modifier in _modifiers)
        {
            modifier.Modify(ref baseScores, context3D, blackboard);
        }

        // 4. Add the final, modified scores to the processor's master score dictionary.
        foreach (var score in baseScores)
        {
            if (finalScores.ContainsKey(score.Key))
            {
                finalScores[score.Key] += score.Value;
            }
        }
    }

    /// <summary>
    /// Child classes MUST implement this method. It contains the core logic for calculating
    /// the raw directional scores before any personality-driven modifications are applied.
    /// </summary>
    protected abstract Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard);

    #region Test Helpers
#if TOOLS
    internal void SetPropagation(SteeringPropagationConfig? config) => _propagation = config;
#endif
    #endregion
}
