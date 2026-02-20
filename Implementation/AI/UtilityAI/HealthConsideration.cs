// --- HealthConsideration.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Health;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

/// <summary>
/// Returns a normalized score based on current health percentage.
/// High health = high score (unless inverted for "low health = high urgency" scenarios).
/// </summary>
[GlobalClass, Tool]
public partial class HealthConsideration : UtilityConsideration
{
    /// <summary>
    /// If true, score is inverted (low health = high score).
    /// Useful for flee/heal considerations.
    /// </summary>
    [Export]
    public bool InvertScore { get; set; } = false;

    protected override float CalculateBaseScore(IBlackboard blackboard)
    {
        // Use IHealth interface for proper abstraction
        if (!blackboard.TryGet<IHealth>(BBDataSig.HealthComponent, out var health) || health == null)
        {
            JmoLogger.Warning(this, "HealthConsideration: Could not find IHealth on Blackboard.");
            return 0f;
        }

        if (health.MaxHealth <= 0)
        {
            return 0f;
        }

        float normalizedHealth = health.CurrentHealth / health.MaxHealth;
        return InvertScore ? 1f - normalizedHealth : normalizedHealth;
    }
}
