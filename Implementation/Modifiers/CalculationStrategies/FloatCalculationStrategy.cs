namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System.Collections.Generic;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;

public partial class FloatCalculationStrategy : Resource, ICalculationStrategy<float>
{
    public float Calculate(float baseValue, List<IModifier<float>> modifiers)
    {
        // --- Stage 1: BaseAdd ---
        foreach (var mod in modifiers)
        {
            // Check if the modifier is the correct type to have a 'Stage'
            if (mod is FloatAttributeModifier fam && fam.Stage == CalculationStage.BaseAdd)
            {
                baseValue = fam.Modify(baseValue);
            }
        }

        // --- Stage 2: PercentAdd ---
        var totalPercentBonus = 0f;
        foreach (var mod in modifiers)
        {
            if (mod is FloatAttributeModifier fam && fam.Stage == CalculationStage.PercentAdd)
            {
                // For this stage, Modify() returns the percentage value itself.
                totalPercentBonus += fam.Modify(0f);
            }
        }

        baseValue *= 1.0f + totalPercentBonus;


        // --- Stage 3: FinalMultiply ---
        foreach (var mod in modifiers)
        {
            if (mod is FloatAttributeModifier fam && fam.Stage == CalculationStage.FinalMultiply)
            {
                baseValue = fam.Modify(baseValue);
            }
        }

        return baseValue;
    }
}
