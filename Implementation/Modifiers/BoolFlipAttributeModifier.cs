namespace Jmodot.Core.Modifiers;

using global::Jmodot.Core.Modifiers;
using Godot.Collections;

/// <summary>
///     Resource for flipping an attributes bool value.
/// </summary>
[GlobalClass]
public partial class BoolFlipAttributeModifier : Resource, IModifier<bool>
{
    [Export] public int Priority { get; private set; }

    [ExportGroup("Tags & Cancellation")]
    [Export]
    public Array<string> Tags { get; private set; } = new();

    [Export] public Array<string> CancelsTags { get; private set; } = new();

    public bool Modify(bool currentValue)
    {
        return !currentValue; // flips bool
    }
}
