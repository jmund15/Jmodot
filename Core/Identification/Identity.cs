namespace Jmodot.Core.Identification;

using Godot.Collections;

/// <summary>
///     A data-driven Resource that defines the specific identity of an object in the game world.
///     Its meaning and relationships are defined by the list of Category resources it belongs to.
/// </summary>
/// <remarks>
///     This decouples the "what" an object is from the systems that interact with it.
///     It acts as a "character sheet" for what an object *is*, defined by the collection
///     of Category resources it belongs to.
/// </remarks>
[GlobalClass]
public sealed partial class Identity : Resource
{
    /// <summary>
    ///     The user-friendly name of the specific identity (e.g., "Elite Grunt", "Health Potion").
    /// </summary>

    [Export]
    public string IdentityName { get; private set; } = "Unnamed Identity";

    /// <summary>
    ///     A list of categories this identity belongs to. An "EliteGrunt" identity might belong to
    ///     the "Enemy", "Ranged", and "Armored" categories, enabling complex and flexible querying by other systems.
    /// </summary>
    [Export]
    public Array<Category> Categories { get; private set; } = new();
}
