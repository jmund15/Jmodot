namespace Jmodot.Core.Identification;

using System.Collections.Generic;
using System.Linq;
using Godot.Collections;
using Implementation.AI.Perception.Strategies;

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
    ///     Optional parent categories forming a hierarchy. A category descends from all its parents
    ///     and their ancestors transitively (e.g., Burn → Fire → Elemental).
    /// </summary>
    [ExportGroup("Hierarchy")]
    [Export]
    public Array<Category> ParentCategories { get; private set; } = new();

    /// <summary>
    ///     Returns true if this category matches <paramref name="target"/> by name,
    ///     or if any ancestor in the <see cref="ParentCategories"/> chain matches.
    /// </summary>
    /// <remarks>
    ///     MATCHING is name-keyed; the WALK GUARD is reference-keyed, and the two must not be
    ///     conflated. Distinct resources legitimately share a name — a CombatTag is authored onto the
    ///     same-named element atom — so a name-keyed guard reads that parent as already-visited and
    ///     severs every broadening above it. Only a repeated REFERENCE is a cycle, and Godot's
    ///     resource cache is what makes an authored self-loop arrive as one. A revisit yields false
    ///     silently: the guard cannot tell a legal converging DAG path from a loop, so cycle and
    ///     duplicate-name loudness belongs to the corpus lint, not to a per-query walk.
    /// </remarks>
    public bool IsOrDescendsFrom(Category? target)
    {
        if (target == null) { return false; }
        if (CategoryName == target.CategoryName) { return true; }

        return DescendsFrom(target, new HashSet<Category>(ReferenceEqualityComparer.Instance) { this });
    }

    private bool DescendsFrom(Category target, HashSet<Category> visitedByReference)
    {
        if (ParentCategories == null) { return false; }

        return ParentCategories.Any(p =>
            p != null
            && visitedByReference.Add(p)
            && (p.CategoryName == target.CategoryName || p.DescendsFrom(target, visitedByReference)));
    }

    /// <summary>
    ///     Adds this category and every ancestor reachable through <see cref="ParentCategories"/>
    ///     into <paramref name="accumulator"/>, which stays CategoryName-keyed via the
    ///     Equals/GetHashCode override — one entry per distinct NAME, as its consumers expect.
    ///     Recursion is guarded separately, by reference, for the reason given on
    ///     <see cref="IsOrDescendsFrom"/>: a same-named ancestor must not halt the walk.
    /// </summary>
    public void CollectSelfAndAncestors(HashSet<Category> accumulator)
    {
        CollectSelfAndAncestors(accumulator, new HashSet<Category>(ReferenceEqualityComparer.Instance));
    }

    private void CollectSelfAndAncestors(HashSet<Category> accumulator, HashSet<Category> visitedByReference)
    {
        if (!visitedByReference.Add(this)) { return; }
        accumulator.Add(this);
        if (ParentCategories == null) { return; }

        foreach (var parent in ParentCategories)
        {
            parent?.CollectSelfAndAncestors(accumulator, visitedByReference);
        }
    }

    /// <summary>
    ///     Optional decay strategy override for perception. When set, sensors will use this
    ///     strategy instead of their default for entities belonging to this category.
    ///     An entity inherits candidates from every category it carries AND those categories'
    ///     ancestors — see <see cref="Identity.ResolvePerceptionDecay"/> for how competing
    ///     candidates are resolved.
    /// </summary>
    [ExportGroup("AI / Perception")]
    [Export]
    public MemoryDecayStrategy? PerceptionDecay { get; private set; }

    /// <summary>
    ///     Ranks this category's <see cref="PerceptionDecay"/> against the other categories an
    ///     entity carries. Highest wins; equal-priority candidates are folded rather than
    ///     arbitrated. Inert when <see cref="PerceptionDecay"/> is null.
    /// </summary>
    [Export]
    public int DecayPriority { get; private set; }

    #region Test Helpers
#if TOOLS

    /// <summary>Sets CategoryName for testing purposes.</summary>
    internal void SetCategoryName(string value) => CategoryName = value;

    /// <summary>Sets PerceptionDecay for testing purposes.</summary>
    internal void SetPerceptionDecay(MemoryDecayStrategy? value) => PerceptionDecay = value;

    /// <summary>Sets DecayPriority for testing purposes.</summary>
    internal void SetDecayPriority(int value) => DecayPriority = value;

    /// <summary>Sets ParentCategories for testing purposes.</summary>
    internal void SetParentCategories(Array<Category> value) => ParentCategories = value;

#endif
    #endregion
}
