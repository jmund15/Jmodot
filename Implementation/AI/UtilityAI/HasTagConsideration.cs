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
        StringName? activeTag = null;

        // Cross-scope read: if the blackboard is wired into a graph, the squad-written tag
        // lives upchain. Local-only fallback preserves test fixtures with no graph wired.
        var graph = blackboard.FindParentGraph();
        if (graph != null)
        {
            if (graph.TryGetUp<StringName>(BBDataSig.ActiveSquadTag, out var fromChain))
            {
                activeTag = fromChain;
            }
        }
        else if (blackboard.TryGet<StringName>(BBDataSig.ActiveSquadTag, out var local))
        {
            activeTag = local;
        }

        return activeTag != null && activeTag == RequiredTag ? 1.0f : 0.0f;
    }
}
