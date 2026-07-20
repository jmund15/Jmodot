namespace Jmodot.Implementation.AI.HSM.TransitionConditions;

using Godot;
using BB;
using Core.AI.BB;
using Core.AI.HSM;
using Core.AI.Squad;
using Core.Shared.Attributes;
using Shared;

/// <summary>
/// HSM transition condition that fires when the squad's published directive matches (or descends from)
/// <see cref="_directive"/>. Export a family category to match any descendant directive; export a leaf
/// for an exact match. Side-effect-free and level-triggered.
/// </summary>
[GlobalClass, Tool]
public partial class SquadDirectiveCondition : TransitionCondition
{
    [Export, RequiredExport]
    private SquadDirectiveDefinition _directive = null!;

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (_directive == null)
        {
            JmoLogger.Warning(this, "SquadDirectiveCondition: _directive is not set.", agent);
            return false;
        }

        // The base signature hands a FLAT local blackboard; the squad directive lives up-chain, so
        // resolve the owning member graph and read via TryGetUp (mirrors HasTagConsideration).
        var graph = bb.FindParentGraph();
        if (graph == null)
        {
            return false;
        }

        if (!graph.TryGetUp<SquadDirectiveDefinition>(BBDataSig.SquadDirective, out var current) || current == null)
        {
            return false;
        }

        return current.IsOrDescendsFrom(_directive);
    }
}
