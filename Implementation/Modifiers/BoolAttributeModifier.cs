namespace Jmodot.Core.Modifiers;

using Godot.Collections;
using Jmodot.Core.Modifiers.StageRules;

/// <summary>
///     Resource for modifying a bool value. Defaults to an override fold (highest-priority value wins).
/// </summary>
[GlobalClass, Tool]
public partial class BoolAttributeModifier : Resource, IBoolModifier, ITaggableModifier
{
    [Export] public BoolModifierStageRule StageRule { get; private set; }

    [Export] public bool Value { get; private set; }
    [Export] public int Priority { get; private set; }

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();

    public BoolAttributeModifier()
    {
        StageRule = new BoolOverrideStageRule();
    }

    public BoolAttributeModifier(bool value, int priority) : this()
    {
        Value = value;
        Priority = priority;
    }
}
