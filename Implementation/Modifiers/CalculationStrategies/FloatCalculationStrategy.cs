namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System.Collections.Generic;
using System.Linq;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;
using Shared;

/// <summary>
///     Folds float modifiers by grouping on their StageRule's StageId, ordering the stages by Order,
///     and reducing each stage. Modifiers arrive pre-sorted by Priority descending (from
///     <c>ModifiableProperty.GetFinalModifiers</c>); GroupBy is order-stable, so that ordering survives
///     into each stage's values — Override relies on index 0 being the highest priority.
/// </summary>
public partial class FloatCalculationStrategy : Resource, ICalculationStrategy<float>
{
    public float Calculate(float baseValue, IReadOnlyList<IModifier<float>> modifiers)
    {
        var typed = modifiers.OfType<IFloatModifier>().ToList();
        var active = typed.Where(m => m.StageRule != null).ToList();
        if (active.Count < typed.Count)
        {
            JmoLogger.Warning(this, $"Dropped {typed.Count - active.Count} float modifier(s) with a null StageRule from the fold — an unset StageRule (e.g. an unauthored .tres slot) silently resolves the stat incorrectly.");
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
