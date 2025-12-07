namespace Jmodot.Core.Modifiers;

using Godot.Collections;
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

    //[ExportGroup("Modification Value")]
    /// <summary>
    /// The value to use for the modification. How this is interpreted depends on the Stage.
    /// For BaseAdd: A flat value (e.g., 10 for +10).
    /// For PercentAdd: A percentage (e.g., 0.1 for +10%).
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
        // The Modify method knows how to interpret its own Value based on its Stage.
        return Stage switch
        {
            // For the BaseAdd stage, it applies its value additively.
            CalculationStage.BaseAdd => currentValue + Value,

            // For the PercentAdd stage, it simply returns its own percentage value (e.g., 0.1)
            // for the pipeline to sum up with other percentage bonuses.
            CalculationStage.PercentAdd => Value,

            // For the FinalMultiply stage, it applies its value multiplicatively.
            CalculationStage.FinalMultiply => currentValue * Value,

            // Default case should never be hit but ensures safety.
            // TODO: error logging could be added here.
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
