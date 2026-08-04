namespace Jmodot.Core.Identification;

using System.Collections.Generic;
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
    /// Builds a synthesized identity from an owner's own name and category fields. This is THE
    /// construction home for an <see cref="IIdentifiable"/> that derives its identity rather than
    /// authoring one: the setters below are deliberately not public, and every synthesizing
    /// implementer routing through here keeps that boundary meaningful.
    /// </summary>
    /// <remarks>
    /// The <paramref name="categories"/> array is SNAPSHOTTED at synthesis: the owner stays its single
    /// author, and later CONTENT mutations of that array do not reach an already-synthesized identity.
    /// Owners re-publish by reassigning through an invalidating setter, which drops the cached identity
    /// and re-synthesizes on the next read.
    /// </remarks>
    public static Identity From(string name, Array<Category> categories)
    {
        var identity = new Identity();
        identity.SetIdentityName(name);
        identity.SetCategories(new Array<Category>(categories));
        return identity;
    }

    /// <summary>Assigns the identity's display name. Non-public: synthesis routes through <see cref="From"/>.</summary>
    internal void SetIdentityName(string value) => IdentityName = value;

    /// <summary>
    /// Assigns the identity's category array and drops the memoized decay resolution.
    /// Non-public: synthesis routes through <see cref="From"/>.
    /// </summary>
    internal void SetCategories(Array<Category> categories)
    {
        Categories = categories;
        _resolvedDecay = null;
        _decayResolved = false;
    }

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
    /// Resolves the perception decay strategy for this identity. Candidates are gathered from every
    /// category this identity carries AND each of their ancestors, then ranked by
    /// <see cref="Category.DecayPriority"/>: the highest priority wins. Candidates tied at the top
    /// priority are FOLDED (the memory decays as slowly as its most persistent contributor) rather
    /// than arbitrated, so the result never depends on authoring array order. Returns null when no
    /// reachable category declares a strategy, leaving the sensor's own default in force.
    /// </summary>
    /// <remarks>
    /// Memoized: this runs per-percept on the perception hot path, and the answer is a pure function
    /// of <see cref="Categories"/> and their (immutable) parent chains.
    /// </remarks>
    public MemoryDecayStrategy? ResolvePerceptionDecay()
    {
        if (_decayResolved) { return _resolvedDecay; }

        _resolvedDecay = ComputePerceptionDecay();
        _decayResolved = true;
        return _resolvedDecay;
    }

    private MemoryDecayStrategy? _resolvedDecay;
    private bool _decayResolved;

    private MemoryDecayStrategy? ComputePerceptionDecay()
    {
        if (Categories == null) { return null; }

        var reachable = new HashSet<Category>();
        foreach (var category in Categories)
        {
            category?.CollectSelfAndAncestors(reachable);
        }

        var topPriority = int.MinValue;
        var winners = new List<MemoryDecayStrategy>();
        foreach (var category in reachable)
        {
            if (category.PerceptionDecay == null) { continue; }
            if (category.DecayPriority < topPriority) { continue; }

            if (category.DecayPriority > topPriority)
            {
                topPriority = category.DecayPriority;
                winners.Clear();
            }
            winners.Add(category.PerceptionDecay);
        }

        if (winners.Count == 0) { return null; }
        if (winners.Count == 1) { return winners[0]; }
        return MaxConfidenceDecay.Over(winners);
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

}
