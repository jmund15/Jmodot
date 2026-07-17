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
    /// Checks whether this identity belongs to the specified category, hierarchically.
    /// Returns true if any of this identity's <see cref="Categories"/> matches the target by name
    /// OR descends from the target via <see cref="Category.ParentCategories"/>. Example: an identity
    /// in category "DirtGround" returns true for a query against "Ground" if DirtGround.ParentCategories
    /// includes Ground (or any ancestor chain that reaches Ground).
    /// </summary>
    public bool HasCategory(Category category)
    {
        if (category == null || Categories == null) { return false; }
        return Categories.Any(c => c?.IsOrDescendsFrom(category) == true);
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

    /// <summary>
    /// Returns a NEW <see cref="Identity"/> carrying this identity's name and a FRESH
    /// <see cref="Categories"/> array containing this identity's categories plus <paramref name="extra"/>.
    /// The <see cref="Category"/> elements are shared (they are immutable, value-equal atoms), so the
    /// clone is safe to mutate at the array level without touching the template. Use this for
    /// per-instance identity stamping (e.g. a summoner's faction on a summoned entity) instead of
    /// <c>Resource.Duplicate()</c>, whose container-sharing semantics are version-fragile.
    /// </summary>
    /// <param name="extra">Additional categories to append; null or empty is tolerated.</param>
    public Identity CloneWithCategories(System.Collections.Generic.IEnumerable<Category>? extra)
    {
        var clone = new Identity { IdentityName = this.IdentityName };
        var newCategories = new Array<Category>();
        if (this.Categories != null)
        {
            foreach (var c in this.Categories) { newCategories.Add(c); }
        }
        if (extra != null)
        {
            foreach (var c in extra) { newCategories.Add(c); }
        }
        clone.Categories = newCategories;
        return clone;
    }

    #region Test Helpers

    /// <summary>Sets IdentityName for testing purposes.</summary>
    internal void SetIdentityName(string value) => IdentityName = value;

    /// <summary>Sets Categories for testing purposes.</summary>
    internal void SetCategories(Array<Category> categories) => Categories = categories;

    #endregion
}
