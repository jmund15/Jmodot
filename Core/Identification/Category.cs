namespace Jmodot.Core.Identification;

using System.Collections.Generic;
using System.Linq;
using Godot.Collections;
using Implementation.AI.Perception.Strategies;
using Implementation.Shared;

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
    ///     Also serves as the equality key — two Category instances with the same CategoryName are considered equal.
    /// </summary>
    [ExportGroup("Identity")]
    [Export]
    public string CategoryName { get; private set; } = "Unnamed Category";

    /// <summary>
    /// Value equality based on CategoryName. Required because Category is used as a Dictionary key
    /// in AIPerceptionManager3D._memoryByCategory. Without this override, two instances loaded from
    /// the same .tres file via different ext_resource chains would be treated as different keys,
    /// silently breaking perception queries.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) { return true; }
        if (obj is not Category other) { return false; }
        return CategoryName == other.CategoryName;
    }

    /// <summary>
    /// Hash code based on CategoryName for consistent Dictionary/HashSet behavior.
    /// </summary>
    public override int GetHashCode()
    {
        return CategoryName?.GetHashCode() ?? 0;
    }

    /// <summary>
    ///     Optional decay strategy override for perception. When set, sensors will use this
    ///     strategy instead of their default for entities belonging to this category.
    /// </summary>
    [ExportGroup("AI / Perception")]
    [Export]
    public MemoryDecayStrategy? PerceptionDecay { get; private set; }

    /// <summary>
    ///     Optional parent categories forming a hierarchy. A category descends from all its parents
    ///     and their ancestors transitively (e.g., Burn → Fire → Elemental).
    /// </summary>
    [Export]
    public Array<Category> ParentCategories { get; private set; } = new();

    /// <summary>
    ///     Returns true if this category matches <paramref name="target"/> by name,
    ///     or if any ancestor in the <see cref="ParentCategories"/> chain matches.
    ///     Uses a visited set (CategoryName-keyed via Equals/GetHashCode override) to guard against cycles.
    ///     Cycles are designer errors and emit a JmoLogger.Warning when detected.
    /// </summary>
    public bool IsOrDescendsFrom(Category? target, HashSet<Category>? visited = null)
    {
        if (target == null) { return false; }
        if (CategoryName == target.CategoryName) { return true; }

        visited ??= new HashSet<Category>();
        if (!visited.Add(this))
        {
            JmoLogger.Warning(this, $"Category cycle detected at '{CategoryName}' while resolving '{target.CategoryName}'. Check ParentCategories chains for self-reference.");
            return false;
        }

        if (ParentCategories == null) { return false; }
        return ParentCategories.Any(p => p?.IsOrDescendsFrom(target, visited) == true);
    }

    #region Test Helpers
#if TOOLS

    /// <summary>Sets CategoryName for testing purposes.</summary>
    internal void SetCategoryName(string value) => CategoryName = value;

    /// <summary>Sets PerceptionDecay for testing purposes.</summary>
    internal void SetPerceptionDecay(MemoryDecayStrategy? value) => PerceptionDecay = value;

    /// <summary>Sets ParentCategories for testing purposes.</summary>
    internal void SetParentCategories(Array<Category> value) => ParentCategories = value;

#endif
    #endregion
}
