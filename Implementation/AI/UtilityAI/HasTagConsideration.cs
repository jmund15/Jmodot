// --- HasTagConsideration.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BB;

/// <summary>
/// Returns 1.0 if the blackboard's active squad tag matches the required tag, 0.0 otherwise.
/// Uses the blackboard's parent-chain lookup, so individual agents automatically check squad tags.
/// </summary>
[GlobalClass]
public partial class HasTagConsideration : UtilityConsideration
{
    [Export]
    public StringName RequiredTag { get; set; } = new("");

    protected override float CalculateBaseScore(IBlackboard blackboard)
    {
        // The "bubble-up" GetVar logic checks local blackboard first, then parent (squad)
        if (!blackboard.TryGet<StringName>(BBDataSig.ActiveSquadTag, out var activeTag))
        {
            return 0f;
        }

        return activeTag == RequiredTag ? 1.0f : 0.0f;
    }
}
