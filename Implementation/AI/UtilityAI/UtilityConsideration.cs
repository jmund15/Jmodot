// --- UtilityConsideration.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;

/// <summary>
/// Abstract base for utility considerations. Evaluates context and returns a normalized score (0-1).
/// Supports a modifier stack for layered adjustments (e.g., affinity-based scaling).
/// </summary>
[GlobalClass, Tool]
public abstract partial class UtilityConsideration : Resource
{
    [Export]
    private Godot.Collections.Array<ConsiderationModifier>? _modifiers;

    /// <summary>
    /// Evaluates the final score of this consideration after applying all modifiers.
    /// This is the primary method called by the UtilitySelector.
    /// </summary>
    /// <param name="blackboard">The agent's blackboard.</param>
    /// <returns>The final, modified utility score, clamped between 0 and 1.</returns>
    public float Evaluate(IBlackboard blackboard)
    {
        // 1. Calculate the objective, raw score.
        float baseScore = CalculateBaseScore(blackboard);

        // 2. Apply each subjective modifier in the stack (null-safe).
        if (_modifiers != null)
        {
            foreach (var modifier in _modifiers)
            {
                if (modifier != null)
                {
                    baseScore = modifier.Modify(baseScore, blackboard);
                }
            }
        }

        // 3. Return the final, clamped score.
        return Mathf.Clamp(baseScore, 0f, 1f);
    }

    /// <summary>
    /// Child classes must implement this method to provide the raw, objective
    /// utility score before any modifiers are applied.
    /// </summary>
    /// <param name="blackboard">The agent's blackboard.</param>
    /// <returns>An unclamped base utility score.</returns>
    protected abstract float CalculateBaseScore(IBlackboard blackboard);
}
