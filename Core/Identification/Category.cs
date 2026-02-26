namespace Jmodot.Core.Identification;

using System.Collections.Generic;
using System.Linq;
using Godot.Collections;

/// <summary>
///     A data-driven Resource representing a high-level, abstract category or "tag".
///     This is a cornerstone of the world's semantic system, allowing for broad-level grouping and querying.
///     For example, this allows an AI to ask "is there an Enemy nearby?" and get a match for any
///     object whose Identity belongs to the "Enemy" category.
/// </summary>
/// <remarks>
///     Crucially, because it is a Resource, it can contain its own data, such as default relationships
///     to other categories, which a simple string tag (like a Godot Group) cannot do.
///     Categories support hierarchical classification via <see cref="ParentCategories"/>, enabling
///     leaf nodes (e.g., "Burn") to descend from broader categories (e.g., "Fire" → "Elemental").
/// </remarks>
[GlobalClass, Tool]
public partial class Category : Resource
{
    /// <summary>
    ///     The user-friendly name of the category for debugging and editor purposes (e.g., "Enemy", "Item", "Consumable").
    /// </summary>
    [Export]
    public string CategoryName { get; private set; } = "Unnamed Category";

    /// <summary>
    ///     Optional parent categories forming a hierarchy. A category descends from all its parents
    ///     and their ancestors transitively (e.g., Burn → Fire → Elemental).
    /// </summary>
    [Export]
    public Array<Category> ParentCategories { get; private set; } = new();

    /// <summary>
    ///     Returns true if this category matches <paramref name="target"/> by name,
    ///     or if any ancestor in the <see cref="ParentCategories"/> chain matches.
    ///     Uses a visited set to guard against cycles.
    /// </summary>
    public bool IsOrDescendsFrom(Category? target, HashSet<Category>? visited = null)
    {
        if (target == null) { return false; }
        if (CategoryName == target.CategoryName) { return true; }

        visited ??= new HashSet<Category>();
        if (!visited.Add(this)) { return false; }

        return ParentCategories?.Any(p => p?.IsOrDescendsFrom(target, visited) == true) == true;
    }

    #region Test Helpers

    /// <summary>Sets CategoryName for testing purposes.</summary>
    internal void SetCategoryName(string value) => CategoryName = value;

    /// <summary>Sets ParentCategories for testing purposes.</summary>
    internal void SetParentCategories(Array<Category> value) => ParentCategories = value;

    #endregion
}
