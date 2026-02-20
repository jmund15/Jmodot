namespace Jmodot.Core.Modifiers;

using System;
using Godot.Collections;
using Jmodot.Implementation.Shared;
using Stats;

/// <summary>
///     Resource for modifying an int value. This is the primary tool
///     a designer will use to create all standard buffs, debuffs, and equipment bonuses in the editor.
///     It fully implements the IModifier contract, including stages, priority, and tags.
/// </summary>
[GlobalClass]
public partial class IntAttributeModifier : Resource, IIntModifier, IModifier<int>
{
    [Export] public CalculationStage Stage { get; private set; } = CalculationStage.BaseAdd;

    [ExportGroup("Modification Value")]
    /// <summary>
    /// The value to use for the modification. How this is interpreted depends on the Stage.
    /// For BaseAdd: A flat value (e.g., 10 for +10).
    /// For PercentAdd: A whole number percentage (e.g., 10 for +10%, 50 for +50%).
    ///   Note: Unlike FloatAttributeModifier, Modify() returns the raw Value (not divided by 100).
    ///   The IntCalculationStrategy compensates with (100 + bonus) / 100f.
    /// For FinalMultiply: A multiplier (e.g., 2 for x2).
    /// </summary>
    [Export] public int Value { get; private set; }
    [Export] public int Priority { get; private set; }

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();
    public int Modify(int currentValue)
    {
        if (!Enum.IsDefined(Stage))
        {
            JmoLogger.Warning(this, $"Unknown CalculationStage '{Stage}' in IntAttributeModifier");
            return currentValue;
        }

        return Stage switch
        {
            CalculationStage.BaseAdd => currentValue + Value,
            CalculationStage.PercentAdd => Value,
            CalculationStage.FinalMultiply => currentValue * Value,

            // Override: return the modifier's value directly, ignoring currentValue.
            // Used for enum-backed int stats with IntOverrideStrategy.
            CalculationStage.Override => Value,

            _ => currentValue
        };
    }
    public IntAttributeModifier()
    {
        // Default constructor for editor use.
    }
    public IntAttributeModifier(int value, CalculationStage stage, int priority)
    {
        Value = value;
        Stage = stage;
        Priority = priority;
    }
}
