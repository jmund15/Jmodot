namespace Jmodot.Core.Stats;

using Mechanics;
using Core.Modifiers.CalculationStrategies;
using GColl = Godot.Collections;
using Jmodot.Implementation.Modifiers.CalculationStrategies;

/// <summary>
///     A comprehensive, data-driven "character sheet" that acts as a library of all available
///     movement capabilities for an actor. It allows a State Machine/Behavior Tree to dynamically select the
///     correct physics data for its current state and actions.
/// </summary>
[GlobalClass]
public partial class EntityStatSheet : Resource
{
    [ExportGroup("Universal Attributes")]
    // The dictionary holds the raw base value.
    [Export]
    public GColl.Dictionary<Attribute, Variant> UniversalAttributes { get; private set; } = new();

    // a separate dictionary to explicitly assign strategies.
    // we can't export templatized interfaces/resources, so for now the user just has to ensure that they're importing a valid Calulation Strategy
    [Export]
    public GColl.Dictionary<Attribute, Resource> UniversalAttributeStrategies { get; private set; } = new();

    /// <summary>
    ///     A dictionary mapping the character's available movement modes to their physics profiles.
    /// </summary>
    [ExportGroup("Contextual Movement")]
    [Export]
    public GColl.Dictionary<StatContext, StatProfile> ContextualProfiles { get; private set; } = new();


    /// <summary>
    ///     A list of all mechanics (like jumps or dashes) this character is capable of performing.
    ///     A State Machine will query this list to find the data for a specific mechanic.
    /// </summary>
    [ExportGroup("Mechanic Library")]
    [Export]
    public GColl.Dictionary<MechanicType, MechanicData> MechanicLibrary { get; private set; } = new();
}
