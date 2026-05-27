namespace Jmodot.Core.Modifiers;

using Godot.Collections;
using Jmodot.Core.Identification;
using Jmodot.Core.Modifiers.StageRules;
using Jmodot.Core.Shared.Attributes;

/// <summary>
///     Resource for modifying a float value. This is the primary tool
///     a designer will use to create all standard buffs, debuffs, and equipment bonuses in the editor.
///     It implements the IModifier contract: a raw Value, a data-driven StageRule, priority, and tags.
/// </summary>
[GlobalClass]
public partial class FloatAttributeModifier : Resource, IFloatModifier, IModifier<float>
{
    /// <summary>The fold rule for this modifier (additive, summed-percent, multiply, override, …).</summary>
    [Export, RequiredExport] public FloatModifierStageRule StageRule { get; private set; } = null!;

    [ExportGroup("Modification Value")]
    /// <summary>
    /// The raw value, interpreted by the StageRule's Reduce:
    /// additive — a flat value (10 for +10); summed-percent — a whole-number percent (10 for +10%);
    /// multiply — a multiplier (2.0 for x2).
    /// </summary>
    [Export] public float Value { get; private set; }
    [Export] public int Priority { get; private set; }

    [ExportGroup("Semantic Classification")]
    /// <summary>
    /// Semantic categories for this modifier (e.g., Fire, Ice).
    /// Used by slot modifiers to target specific modifier types.
    /// Warning: This is a shared Godot Array on a Resource — consumers should
    /// create a defensive copy via <c>new Array&lt;Category&gt;(source)</c> before mutating.
    /// </summary>
    [Export] public Array<Category> SemanticCategories { get; private set; } = new();

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();

    public FloatAttributeModifier()
    {
        // Default constructor for editor use.
    }
    public FloatAttributeModifier(float value, FloatModifierStageRule stageRule, int priority)
    {
        Value = value;
        StageRule = stageRule;
        Priority = priority;
    }
}
