namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;
using Core.Stats;
using Godot;
using Jmodot.Core.Stats;

/// <summary>
/// Float calculation strategy that caps the total percentage bonus applied.
/// Use this for stats where % bonuses should be limited (e.g., max 100% bonus to charge speed).
/// </summary>
[GlobalClass]
public partial class PercentCappedFloatCalculationStrategy : Resource, ICalculationStrategy<float>
{
    /// <summary>
    /// Maximum total percentage bonus (as a decimal).
    /// E.g., 1.0 means max +100% bonus, 0.5 means max +50%.
    /// Defaults to float.MaxValue (no cap).
    /// </summary>
    [Export] public float MaxPercentBonus { get; set; } = float.MaxValue;

    /// <summary>
    /// Optional cap on the final calculated value.
    /// Applied after all calculations. Defaults to float.MaxValue (no cap).
    /// </summary>
    [Export] public float MaxFinalValue { get; set; } = float.MaxValue;

    /// <summary>
    /// Optional minimum for the final calculated value.
    /// Applied after all calculations. Defaults to float.MinValue (no minimum).
    /// </summary>
    [Export] public float MinFinalValue { get; set; } = float.MinValue;

    public float Calculate(float baseValue, IReadOnlyList<IModifier<float>> modifiers)
    {
        var activeModifiers = modifiers.OfType<IFloatModifier>().ToList();

        // --- Stage 1: BaseAdd ---
        foreach (var mod in activeModifiers)
        {
            if (mod.Stage == CalculationStage.BaseAdd)
            {
                baseValue = mod.Modify(baseValue);
            }
        }

        // --- Stage 2: PercentAdd (with capping) ---
        var totalPercentBonus = 0f;
        foreach (var mod in activeModifiers)
        {
            if (mod.Stage == CalculationStage.PercentAdd)
            {
                totalPercentBonus += mod.Modify(0f);
            }
        }

        // Cap the percentage bonus
        totalPercentBonus = Math.Min(totalPercentBonus, MaxPercentBonus);

        baseValue *= 1.0f + totalPercentBonus;

        // --- Stage 3: FinalMultiply ---
        foreach (var mod in activeModifiers)
        {
            if (mod.Stage == CalculationStage.FinalMultiply)
            {
                baseValue = mod.Modify(baseValue);
            }
        }

        // Clamp final value
        return Math.Clamp(baseValue, MinFinalValue, MaxFinalValue);
    }
}
