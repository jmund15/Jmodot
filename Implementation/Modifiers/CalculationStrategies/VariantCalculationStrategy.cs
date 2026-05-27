namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System.Collections.Generic;
using System.Linq;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;
using Shared;

/// <summary>
///     Folds Variant modifiers by grouping on their StageRule's StageId, ordering by Order, and reducing
///     each stage. Replaces the old priority-override-only Variant strategy.
/// </summary>
public partial class VariantCalculationStrategy : Resource, ICalculationStrategy<Variant>
{
    public Variant Calculate(Variant baseValue, IReadOnlyList<IModifier<Variant>> modifiers)
    {
        var typed = modifiers.OfType<IVariantModifier>().ToList();
        var active = typed.Where(m => m.StageRule != null).ToList();
        if (active.Count < typed.Count)
        {
            JmoLogger.Warning(this, $"Dropped {typed.Count - active.Count} Variant modifier(s) with a null StageRule from the fold — an unset StageRule silently resolves the stat incorrectly.");
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
