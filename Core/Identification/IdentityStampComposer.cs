namespace Jmodot.Core.Identification;

using System.Collections.Generic;
using Implementation.Shared;

/// <summary>
/// Composes an entity's runtime-expressed <see cref="Identity"/> from an authored base plus keyed
/// category contributions. Holders own one of these and forward their capability surface to it.
/// </summary>
/// <remarks>
/// The authored base is supplied per call and never retained as a clone source, so every clone
/// derives from the holder's current authored home rather than from a prior clone — compounding
/// (a pooled entity accumulating categories across lives) is not expressible through this API.
/// Composition dedups by <see cref="Category"/> name-equality, first occurrence winning: two
/// instances sharing a CategoryName are indistinguishable to every consumer, so a duplicate is noise.
/// Dedup extends across the authored base — the clone appends the composed list onto a copy of the
/// base's categories, so a contribution the base already carries is dropped rather than double-listed.
/// This makes the composed list base-relative; change detection stays correct because the base itself
/// participates by reference.
/// </remarks>
public sealed class IdentityStampComposer
{
    private readonly KeyedContributionChannels<Category> _channels = new();
    private readonly List<Category> _composed = new();
    private Identity? _lastBase;

    /// <summary>The composed identity, or null when no channel contributes — the holder then falls
    /// back to its authored base, making restore reference-identical rather than value-equal.</summary>
    public Identity? Expressed { get; private set; }

    /// <summary>Registers (or replaces) one owner's category contribution and returns the resulting
    /// expressed identity.</summary>
    public Identity? StampCategories(object key, IReadOnlyList<Category> categories, Identity authoredBase)
    {
        this._channels.Set(key, categories);
        return this.Recompose(authoredBase);
    }

    /// <summary>Retracts one owner's contribution. An unknown key composes to the same result.</summary>
    public Identity? ClearStamp(object key, Identity authoredBase)
    {
        this._channels.Clear(key);
        return this.Recompose(authoredBase);
    }

    private Identity? Recompose(Identity authoredBase)
    {
        var composed = new List<Category>();
        this._channels.ComposeOnto(composed);
        Normalize(composed, authoredBase);

        if (ReferenceEquals(this._lastBase, authoredBase) && SameNames(this._composed, composed))
        {
            return this.Expressed;
        }

        this._lastBase = authoredBase;
        this._composed.Clear();
        this._composed.AddRange(composed);
        this.Expressed = composed.Count == 0 ? null : authoredBase.CloneWithCategories(composed);
        return this.Expressed;
    }

    /// <summary>
    /// Drops nulls, name-duplicates and anything the authored base already carries, first occurrence
    /// winning. The base filter matters because the clone APPENDS this list onto a copy of the base's
    /// own categories, so an overlap would double-list.
    /// </summary>
    private static void Normalize(List<Category> categories, Identity authoredBase)
    {
        var seen = new HashSet<string>();
        if (authoredBase.Categories != null)
        {
            foreach (var category in authoredBase.Categories)
            {
                if (category != null) { seen.Add(category.CategoryName); }
            }
        }

        for (int i = 0; i < categories.Count;)
        {
            var category = categories[i];
            if (category == null || !seen.Add(category.CategoryName)) { categories.RemoveAt(i); }
            else { i++; }
        }
    }

    private static bool SameNames(List<Category> left, List<Category> right)
    {
        if (left.Count != right.Count) { return false; }

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i].CategoryName != right[i].CategoryName) { return false; }
        }

        return true;
    }
}
