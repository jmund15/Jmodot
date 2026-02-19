namespace Jmodot.Core.Modifiers;

using System;
using Godot.Collections;
using Jmodot.Implementation.Shared;
using Stats;

/// <summary>
///     Resource for modifying a float value. This is the primary tool
///     a designer will use to create all standard buffs, debuffs, and equipment bonuses in the editor.
///     It fully implements the IModifier contract, including stages, priority, and tags.
/// </summary>
[GlobalClass]
public partial class FloatAttributeModifier : Resource, IFloatModifier, IModifier<float>
{
    [Export] public CalculationStage Stage { get; private set; } = CalculationStage.BaseAdd;

    [ExportGroup("Modification Value")]
    /// <summary>
    /// The value to use for the modification. How this is interpreted depends on the Stage.
    /// For BaseAdd: A flat value (e.g., 10 for +10).
    /// For PercentAdd: A whole number percentage (e.g., 10 for +10%, 50 for +50%).
    ///   Note: Modify() returns Value/100f (the decimal fraction). The strategy sums these fractions.
    /// For FinalMultiply: A multiplier (e.g., 2.0 for x2).
    /// </summary>
    [Export]
    public float Value { get; private set; }
    [Export] public int Priority { get; private set; }

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();
    public float Modify(float currentValue)
    {
        if (!Enum.IsDefined(Stage))
        {
            JmoLogger.Warning(this, $"Unknown CalculationStage '{Stage}' in FloatAttributeModifier");
            return currentValue;
        }

        return Stage switch
        {
            CalculationStage.BaseAdd => currentValue + Value,
            CalculationStage.PercentAdd => Value / 100f,
            CalculationStage.FinalMultiply => currentValue * Value,
            _ => currentValue
        };
    }
    public FloatAttributeModifier()
    {
        // Default constructor for editor use.
    }
    public FloatAttributeModifier(float value, CalculationStage stage, int priority)
    {
        Value = value;
        Stage = stage;
        Priority = priority;
    }
}
