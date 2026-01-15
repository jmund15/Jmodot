namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System;
using System.Collections.Generic;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;
using Core.Stats;
using Godot;

/// <summary>
/// Float calculation strategy that clamps the final value between min and max bounds.
/// Use this for stats that should never exceed certain values (e.g., attack speed capped at 5).
/// </summary>
[GlobalClass]
public partial class ClampedFloatCalculationStrategy : Resource, ICalculationStrategy<float>
{
    /// <summary>
    /// Minimum allowed final value. Defaults to float.MinValue (no minimum).
    /// </summary>
    [Export] public float MinValue { get; set; } = float.MinValue;

    /// <summary>
    /// Maximum allowed final value. Defaults to float.MaxValue (no maximum).
    /// </summary>
    [Export] public float MaxValue { get; set; } = float.MaxValue;

    private readonly FloatCalculationStrategy _baseStrategy = new();

    public float Calculate(float baseValue, IReadOnlyList<IModifier<float>> modifiers)
    {
        // Use base calculation
        var result = _baseStrategy.Calculate(baseValue, modifiers);

        // Clamp to bounds
        return Math.Clamp(result, MinValue, MaxValue);
    }
}
