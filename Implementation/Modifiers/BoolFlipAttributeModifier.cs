namespace Jmodot.Core.Modifiers;

using global::Jmodot.Core.Modifiers;
using Godot.Collections;
using Stats;

/// <summary>
///     Resource for flipping an attributes bool value.
/// </summary>
[GlobalClass]
public partial class BoolFlipAttributeModifier : Resource, IModifier<bool>
{
    [Export] public int Priority { get; private set; }

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();


    public bool Modify(bool currentValue)
    {
        return !currentValue; // flips bool
    }
}
