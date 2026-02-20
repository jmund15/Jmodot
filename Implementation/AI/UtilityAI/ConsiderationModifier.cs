// --- ConsiderationModifier.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;

/// <summary>
/// Abstract base for consideration modifiers. Applied in sequence to adjust utility scores.
/// Use for personality-based scaling (e.g., Fear affinity increases flee score).
/// </summary>
[GlobalClass]
public abstract partial class ConsiderationModifier : Resource
{
    /// <summary>
    /// Modifies the base score based on agent state or personality.
    /// </summary>
    /// <param name="baseScore">The current score from the consideration or previous modifier.</param>
    /// <param name="blackboard">The agent's blackboard for context.</param>
    /// <returns>The modified score.</returns>
    public abstract float Modify(float baseScore, IBlackboard blackboard);
}
