namespace Jmodot.Core.Modifiers;

using Godot.Collections;
using Jmodot.Core.Modifiers.StageRules;

/// <summary>
///     Resource for flipping an attribute's bool value. Folds via a flip stage (toggles once per modifier);
///     <see cref="Value" /> is unused by the flip rule but present to satisfy the IBoolModifier contract.
/// </summary>
[GlobalClass, Tool]
public partial class BoolFlipAttributeModifier : Resource, IBoolModifier, ITaggableModifier
{
    [Export] public BoolModifierStageRule StageRule { get; private set; }

    [Export] public bool Value { get; private set; }
    [Export] public int Priority { get; private set; }

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();

    public BoolFlipAttributeModifier()
    {
        StageRule = new BoolFlipStageRule();
    }

    public BoolFlipAttributeModifier(int priority) : this()
    {
        Priority = priority;
    }
}
