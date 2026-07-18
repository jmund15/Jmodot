// --- HasTagConsideration.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;

/// <summary>
/// Returns 1.0 if the tag stored under <see cref="_tagSourceKey"/> matches <see cref="RequiredTag"/>,
/// 0.0 otherwise. Uses the blackboard's parent-chain lookup, so individual agents automatically
/// read a tag published on an ancestor scope (e.g. a squad graph).
/// </summary>
[GlobalClass]
public partial class HasTagConsideration : UtilityConsideration
{
    [Export]
    public StringName RequiredTag { get; set; } = new("");

    /// <summary>Blackboard key the active tag is read from (up the parent chain, or locally as a fallback).</summary>
    [Export]
    private StringName _tagSourceKey = new("ActiveTag");

    protected override float CalculateBaseScore(IBlackboard blackboard)
    {
        StringName? activeTag = null;

        // Cross-scope read: if the blackboard is wired into a graph, the tag lives upchain.
        // Local-only fallback preserves test fixtures with no graph wired.
        var graph = blackboard.FindParentGraph();
        if (graph != null)
        {
            if (graph.TryGetUp<StringName>(_tagSourceKey, out var fromChain))
            {
                activeTag = fromChain;
            }
        }
        else if (blackboard.TryGet<StringName>(_tagSourceKey, out var local))
        {
            activeTag = local;
        }

        return activeTag != null && activeTag == RequiredTag ? 1.0f : 0.0f;
    }
}
