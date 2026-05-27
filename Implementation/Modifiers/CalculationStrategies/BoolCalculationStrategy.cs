namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System.Collections.Generic;
using System.Linq;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;
using Shared;

/// <summary>
///     Folds bool modifiers by grouping on their StageRule's StageId, ordering by Order, and reducing
///     each stage. Replaces the old priority-override-only bool strategy; with a single modifier the
///     override fold is bit-identical to the prior behaviour.
/// </summary>
public partial class BoolCalculationStrategy : Resource, ICalculationStrategy<bool>
{
    public bool Calculate(bool baseValue, IReadOnlyList<IModifier<bool>> modifiers)
    {
        var typed = modifiers.OfType<IBoolModifier>().ToList();
        var active = typed.Where(m => m.StageRule != null).ToList();
        if (active.Count < typed.Count)
        {
            JmoLogger.Warning(this, $"Dropped {typed.Count - active.Count} bool modifier(s) with a null StageRule from the fold — an unset StageRule silently resolves the stat incorrectly.");
        }
        if (active.Count == 0) { return baseValue; }

        var running = baseValue;
        foreach (var group in active.GroupBy(m => m.StageRule.StageId)
                                    .OrderBy(g => g.First().StageRule.Order))
        {
            running = group.First().StageRule.Reduce(running, group.Select(m => m.Value).ToList());
        }
        return running;
    }
}
