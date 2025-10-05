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
    [Export] public string VelocityProfileName { get; private set; } = "Unnamed Velocity Profile";

    /// <summary>
    ///     A library of all physics attributes for this profile. Movement Strategies
    ///     will query this dictionary for the specific attributes they need to perform
    ///     their calculations.
    /// </summary>
    [Export]
    public Dictionary<Attribute, Variant> Attributes { get; private set; } = new();

    // OPTIONAL: Override the calculation strategy for specific attributes in this profile.
    // If an attribute is not in this dictionary, the StatController will use a default.
    [Export]
    public Dictionary<Attribute, Resource> AttributeStrategies { get; private set; } = new();
}
