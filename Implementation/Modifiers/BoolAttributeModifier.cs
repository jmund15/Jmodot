namespace Jmodot.Core.Modifiers;

using Godot.Collections;

/// <summary>
///     Resource for modifying a bool value.
/// </summary>
[GlobalClass]
public partial class BoolAttributeModifier : Resource, IModifier<bool>
{
    [Export]
    public bool Value { get; private set; }
    [Export] public int Priority { get; private set; }

    [ExportGroup("EffectTags & Cancellation")]
    [Export] public Array<string> EffectTags { get; private set; } = new();
    [Export] public Array<string> CancelsEffectTags { get; private set; } = new();
    [Export] public Array<string> ContextTags { get; private set; } = new();
    [Export] public Array<string> RequiredContextTags { get; private set; } = new();

    public bool Modify(bool currentValue)
    {
        return Value; // bool just overrides
    }
}

