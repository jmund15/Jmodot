namespace Jmodot.Implementation.Modifiers;

using Jmodot.Core.Modifiers;
using Godot.Collections;

/// <summary>
/// A general-purpose modifier that directly overrides the value of any Variant stat.
/// Its effect is determined by its Priority relative to other override modifiers.
/// </summary>
[GlobalClass]
public partial class VariantOverrideModifier : Resource, IModifier<Variant>
{
    [Export] public Variant Value { get; private set; }
    [Export] public int Priority { get; private set; }
    [Export] public Array<string> Tags { get; private set; } = new();
    [Export] public Array<string> CancelsTags { get; private set; } = new();

    /// <summary>
    /// For an override, the Modify method simply ignores the input value
    /// and returns its own configured Value.
    /// </summary>
    public Variant Modify(Variant currentValue)
    {
        return Value;
    }
}
