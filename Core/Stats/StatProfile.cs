namespace Jmodot.Core.Stats;

using Godot.Collections;
using Jmodot.Implementation.Modifiers.CalculationStrategies;

/// <summary>
///     A data-driven Resource that defines a set of independent, raw physics properties.
///     Its sole purpose is to act as a "dumb" data container. The actual movement
///     behavior is determined by a "Movement Strategy" that interprets this data.
/// </summary>
[GlobalClass]
public sealed partial class StatProfile : Resource
{
    // a clear list of targeted attribute modifications.
    [Export]
    public Dictionary<Attribute, Resource> Modifiers { get; private set; } = new();

    // // --- Random Attribute Modifiers (Attribute is chosen at runtime) ---
    // [Export(PropertyHint.TypeString, "RandomAttributeModifierGroup")]
    // public Array<RandomAttributeModifierGroup> RandomAttributeModifiers { get; private set; } = new();
}
