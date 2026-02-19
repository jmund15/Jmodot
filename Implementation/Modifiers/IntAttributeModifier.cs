namespace Jmodot.Core.Modifiers;

using Godot.Collections;
using Stats;

/// <summary>
///     Resource for modifying a float value. This is the primary tool
///     a designer will use to create all standard buffs, debuffs, and equipment bonuses in the editor.
///     It fully implements the IModifier contract, including stages, priority, and tags.
/// </summary>
[GlobalClass]
public partial class IntAttributeModifier : Resource, IIntModifier, IModifier<int>
{
    [Export] public CalculationStage Stage { get; private set; } = CalculationStage.BaseAdd;

    //[ExportGroup("Modification Value")]
    /// <summary>
    /// The value to use for the modification. How this is interpreted depends on the Stage.
    /// For BaseAdd: A flat value (e.g., 10 for +10).
    /// For PercentAdd: A whole number percentage (e.g., 10 for +10%, 50 for +50%).
    /// For FinalMultiply: A multiplier (e.g., 2 for x2).
    /// </summary>
    [Export]
    public int Value { get; private set; }
    [Export] public int Priority { get; private set; }

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();
    public int Modify(int currentValue)
    {
        // The Modify method knows how to interpret its own Value based on its Stage.
        return Stage switch
        {
            // For the BaseAdd stage, it applies its value additively.
            CalculationStage.BaseAdd => currentValue + Value,

            // For the PercentAdd stage, return the whole number percentage (e.g., 10 for +10%).
            // The IntCalculationStrategy applies this as: baseValue * ((100 + totalBonus) / 100).
            CalculationStage.PercentAdd => Value,

            // For the FinalMultiply stage, it applies its value multiplicatively.
            CalculationStage.FinalMultiply => currentValue * Value,

            // Override: return the modifier's value directly, ignoring currentValue.
            // Used for enum-backed int stats with IntOverrideStrategy.
            CalculationStage.Override => Value,

            // Default case should never be hit but ensures safety.
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
