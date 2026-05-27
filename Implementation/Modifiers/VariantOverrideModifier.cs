namespace Jmodot.Implementation.Modifiers;

using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.StageRules;
using Godot.Collections;

/// <summary>
/// A general-purpose modifier that directly overrides the value of any Variant stat.
/// Its effect is determined by its Priority relative to other override modifiers.
/// </summary>
[GlobalClass]
public partial class VariantOverrideModifier : Resource, IVariantModifier, IModifier<Variant>
{
    [Export] public VariantModifierStageRule StageRule { get; private set; }

    [Export] public Variant Value { get; private set; }
    [Export] public int Priority { get; private set; }
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();

    public VariantOverrideModifier()
    {
        StageRule = new VariantOverrideStageRule();
    }
}
