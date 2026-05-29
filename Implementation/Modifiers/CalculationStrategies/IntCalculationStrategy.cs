namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System.Collections.Generic;
using System.Linq;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;
using Shared;

/// <summary>
///     Folds int modifiers by grouping on their StageRule's StageId, ordering by Order, and reducing
///     each stage. See <see cref="FloatCalculationStrategy" /> for the priority/ordering contract.
/// </summary>
public partial class IntCalculationStrategy : Resource, ICalculationStrategy<int>
{
    public int Calculate(int baseValue, IReadOnlyList<IModifier<int>> modifiers)
    {
        var typed = modifiers.OfType<IIntModifier>().ToList();
        var active = typed.Where(m => m.StageRule != null).ToList();
        if (active.Count < typed.Count)
        {
            JmoLogger.Warning(this, $"Dropped {typed.Count - active.Count} int modifier(s) with a null StageRule from the fold — an unset StageRule (e.g. an unauthored .tres slot) silently resolves the stat incorrectly.");
        }
        if (active.Count == 0) { return baseValue; }

        var running = baseValue;
        foreach (var group in active.GroupBy(m => m.StageRule.StageId)
                                    .OrderBy(g => g.First().StageRule.Order))
        {
            var rule = group.First().StageRule;
            rule.Validate();
            running = rule.Reduce(running, group.Select(m => m.Value).ToList());
        }
        return running;
    }
}
