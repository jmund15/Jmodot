namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System.Collections.Generic;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;

public partial class FloatCalculationStrategy : VariantDefaultCalculationStrategy
{
    public override Variant Calculate(Variant baseValue, List<IModifier<Variant>> modifiers)
    {
        if (baseValue.VariantType != Variant.Type.Float)
        {
            GD.PrintErr("FloatCalculationStrategy used on a non-float base value. Returning base value.");
            return baseValue;
        }

        var currentValue = baseValue.AsSingle();

        // --- Stage 1: BaseAdd ---
        foreach (var mod in modifiers)
        {
            // Check if the modifier is the correct type to have a 'Stage'
            if (mod is FloatAttributeModifier fam && fam.Stage == CalculationStage.BaseAdd)
            {
                currentValue = fam.Modify(currentValue).AsSingle();
            }
        }

        // --- Stage 2: PercentAdd ---
        var totalPercentBonus = 0f;
        foreach (var mod in modifiers)
        {
            if (mod is FloatAttributeModifier fam && fam.Stage == CalculationStage.PercentAdd)
            {
                // For this stage, Modify() returns the percentage value itself.
                totalPercentBonus += fam.Modify(0f).AsSingle();
            }
        }

        currentValue *= 1.0f + totalPercentBonus;


        // --- Stage 3: FinalMultiply ---
        foreach (var mod in modifiers)
        {
            if (mod is FloatAttributeModifier fam && fam.Stage == CalculationStage.FinalMultiply)
            {
                currentValue = fam.Modify(currentValue).AsSingle();
            }
        }

        return currentValue;
    }
}
