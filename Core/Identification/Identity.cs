namespace Jmodot.Core.Identification;

using System.Linq;
using Godot.Collections;
using Implementation.AI.Perception.Strategies;

/// <summary>
///     A data-driven Resource that defines the specific identity of an object in the game world.
///     Its meaning and relationships are defined by the list of Category resources it belongs to.
/// </summary>
/// <remarks>
///     This decouples the "what" an object is from the systems that interact with it.
///     It acts as a "character sheet" for what an object *is*, defined by the collection
///     of Category resources it belongs to.
/// </remarks>
[GlobalClass, Tool]
public partial class Identity : Resource
{
    /// <summary>
    ///     The user-friendly name of the specific identity (e.g., "Elite Grunt", "Health Potion").
    /// </summary>
    [Export] public string IdentityName { get; private set; } = "Unnamed Identity";

    /// <summary>
    ///     A list of categories this identity belongs to. An "EliteGrunt" identity might belong to
    ///     the "Enemy", "Ranged", and "Armored" categories, enabling complex and flexible querying by other systems.
    /// </summary>
    [Export] public Array<Category> Categories { get; private set; } = new();

    /// <summary>
    /// Checks whether this identity belongs to the specified category.
    /// Matches by CategoryName for consistency with string-safe comparisons across loaded resources.
    /// </summary>
    /// <remarks>
    /// Both sides of the comparison must be non-null. The previous `c?.X == category.X`
    /// pattern returned true on null==null, silently treating a null entry in Categories
    /// as matching a Category whose name was also null — a false positive that masked
    /// designer misconfiguration of partially-filled .tres resources. Fix consumed by
    /// PushinPotions PR #55 (trail subsystem review, ASK#8).
    /// </remarks>
    public bool HasCategory(Category category)
    {
        if (category == null || Categories == null) { return false; }
        return Categories.Any(c => c != null && c.CategoryName == category.CategoryName);
    }

    /// <summary>
    /// Resolves the perception decay strategy from this identity's categories.
    /// Returns the PerceptionDecay from the first category that has one, or null if none do.
    /// </summary>
    public MemoryDecayStrategy? ResolvePerceptionDecay()
    {
        if (Categories == null) { return null; }
        foreach (var category in Categories)
        {
            if (category?.PerceptionDecay != null) { return category.PerceptionDecay; }
        }
        return null;
    }

    #region Test Helpers

    /// <summary>Sets IdentityName for testing purposes.</summary>
    internal void SetIdentityName(string value) => IdentityName = value;

    /// <summary>Sets Categories for testing purposes.</summary>
    internal void SetCategories(Array<Category> categories) => Categories = categories;

    #endregion
}
