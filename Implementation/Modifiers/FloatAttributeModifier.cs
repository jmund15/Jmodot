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
[GlobalClass, Tool]
public partial class FloatAttributeModifier : AttributeModifier, IFloatModifier
{
    /// <summary>
    /// The raw value, interpreted by <see cref="StageRule"/>:
    /// additive — a flat value (10 for +10); summed-percent — a whole-number percent (10 for +10%);
    /// multiply — a multiplier (2.0 for x2).
    /// </summary>
    [Export] public float Value { get; private set; }

    /// <summary>The fold rule for this modifier (additive, summed-percent, multiply, override, …).</summary>
    [Export, RequiredExport] public FloatModifierStageRule StageRule { get; private set; } = CanonicalStageRules.FloatAdditive;

    /// <summary>
    /// Semantic categories for this modifier (e.g., Fire, Ice).
    /// Used by slot modifiers to target specific modifier types.
    /// Warning: This is a shared Godot Array on a Resource — consumers must
    /// create a defensive copy via <c>new Array&lt;Category&gt;(source)</c> before mutating.
    /// </summary>
    [ExportGroup("Semantic Classification")]
    [Export] public Array<Category> SemanticCategories { get; private set; } = new();

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

    /// <summary>
    /// Returns a copy of this modifier carrying <paramref name="value"/> in place of <see cref="Value"/>.
    /// Callers scaling a modifier MUST use this rather than the three-argument constructor: the constructor
    /// carries only value/rule/priority, so a rebuild through it drops the tag arrays that decide whether the
    /// modifier is cancelled or context-gated, and the fold then applies a modifier the author gated off.
    /// Every array is copied, never aliased — the source's arrays are mutable and shared.
    /// </summary>
    public FloatAttributeModifier WithValue(float value) => new(value, StageRule, Priority)
    {
        EffectTags = new Array<string>(EffectTags),
        CancelsEffectTags = new Array<string>(CancelsEffectTags),
        ContextTags = new Array<string>(ContextTags),
        RequiredContextTags = new Array<string>(RequiredContextTags),
        SemanticCategories = new Array<Category>(SemanticCategories),
    };
}
