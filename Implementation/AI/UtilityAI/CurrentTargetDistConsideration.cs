// --- CurrentTargetDistConsideration.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BB;

/// <summary>
/// Returns a normalized score based on distance to current target.
/// Score decreases as distance increases (closer = higher score by default).
/// </summary>
[GlobalClass, Tool]
public partial class CurrentTargetDistConsideration : UtilityConsideration
{
    /// <summary>
    /// Maximum distance for normalization. Distances beyond this return 0 (or 1 if inverted).
    /// </summary>
    [Export(PropertyHint.Range, "1,1000,1")]
    public float MaxDistance { get; set; } = 100f;

    /// <summary>
    /// If true, score increases with distance (farther = higher score).
    /// </summary>
    [Export]
    public bool InvertScore { get; set; } = false;

    protected override float CalculateBaseScore(IBlackboard context)
    {
        // Get agent position
        if (!context.TryGet<Node2D>(BBDataSig.Agent, out var agent) || agent == null)
        {
            return 0f;
        }

        // Get current target
        if (!context.TryGet<Node2D>(BBDataSig.CurrentTarget, out var target) || target == null)
        {
            return 0f;
        }

        float distance = agent.GlobalPosition.DistanceTo(target.GlobalPosition);
        float normalizedScore = Mathf.Clamp(1f - (distance / MaxDistance), 0f, 1f);

        return InvertScore ? 1f - normalizedScore : normalizedScore;
    }
}
