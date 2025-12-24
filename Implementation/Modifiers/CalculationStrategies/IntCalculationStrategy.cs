namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System.Collections.Generic;
using System.Linq;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;
using Core.Stats;

public partial class IntCalculationStrategy : Resource, ICalculationStrategy<int>
{
    public int Calculate(int baseValue, IReadOnlyList<IModifier<int>> modifiers)
    {
        var activeModifiers = modifiers.OfType<IIntModifier>();
        // --- Stage 1: BaseAdd ---
        foreach (var mod in activeModifiers)
        {
            // Check if the modifier is the correct type to have a 'Stage'
            if (mod.Stage == CalculationStage.BaseAdd)
            {
                baseValue = mod.Modify(baseValue);
            }
        }

        // --- Stage 2: PercentAdd ---
        var totalPercentBonus = 0;
        foreach (var mod in activeModifiers)
        {
            if (mod.Stage == CalculationStage.PercentAdd)
            {
                // For this stage, Modify() returns the percentage value itself.
                totalPercentBonus += mod.Modify(0);
            }
        }

        baseValue = Mathf.RoundToInt(baseValue *  ((100 + totalPercentBonus) / 100f) );


        // --- Stage 3: FinalMultiply ---
        foreach (var mod in activeModifiers)
        {
            if (mod.Stage == CalculationStage.FinalMultiply)
            {
                baseValue = mod.Modify(baseValue);
            }
        }

        return baseValue;
    }
}
