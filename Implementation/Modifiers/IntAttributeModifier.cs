namespace Jmodot.Core.Modifiers;

using Godot.Collections;
using Jmodot.Core.Modifiers.StageRules;
using Jmodot.Core.Shared.Attributes;

/// <summary>
///     Resource for modifying an int value. This is the primary tool
///     a designer will use to create all standard buffs, debuffs, and equipment bonuses in the editor.
///     It implements the IModifier contract: a raw Value, a data-driven StageRule, priority, and tags.
/// </summary>
[GlobalClass]
public partial class IntAttributeModifier : Resource, IIntModifier, IModifier<int>
{
    /// <summary>The fold rule for this modifier (additive, summed-percent, multiply, override, …).</summary>
    [Export, RequiredExport] public IntModifierStageRule StageRule { get; private set; } = null!;

    [ExportGroup("Modification Value")]
    /// <summary>
    /// The raw value, interpreted by the StageRule's Reduce:
    /// additive — a flat value (10 for +10); summed-percent — a whole-number percent (10 for +10%);
    /// multiply — a multiplier (2 for x2); override — the replacement value.
    /// </summary>
    [Export] public int Value { get; private set; }
    [Export] public int Priority { get; private set; }

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();

    public IntAttributeModifier()
    {
        // Default constructor for editor use.
    }
    public IntAttributeModifier(int value, IntModifierStageRule stageRule, int priority)
    {
        Value = value;
        StageRule = stageRule;
        Priority = priority;
    }
}
