namespace Jmodot.Implementation.Shared;

using System.Collections.Generic;

/// <summary>
/// Keyed contribution bookkeeping: N outside owners each register an ordered item list, and the
/// holder composes them. Keys are owner objects compared by reference, so two owners that happen to
/// be <see cref="object.Equals(object)"/>-equal still hold distinct channels and no owner needs to
/// implement equality to register one.
/// </summary>
/// <typeparam name="T">The contributed item type. The channel machine never inspects it.</typeparam>
public sealed class KeyedContributionChannels<T>
{
    /// <summary>Insertion-ordered, never a Dictionary: composition order is observable at every
    /// consumer, and Dictionary enumeration order is unspecified after a removal + re-add.</summary>
    private readonly List<(object Key, IReadOnlyList<T> Items)> _channels = new();

    /// <summary>The number of registered channels.</summary>
    public int Count => this._channels.Count;

    /// <summary>Registers one owner's contribution, replacing in place (keeping its position) when
    /// the key is already registered and appending otherwise.</summary>
    public void Set(object key, IReadOnlyList<T> items)
    {
        int index = this.IndexOf(key);
        if (index >= 0) { this._channels[index] = (key, items); }
        else { this._channels.Add((key, items)); }
    }

    /// <summary>Retracts one owner's contribution. Returns false when the key held no channel, so
    /// holders can gate a recompose on an actual change.</summary>
    public bool Clear(object key)
    {
        int index = this.IndexOf(key);
        if (index < 0) { return false; }

        this._channels.RemoveAt(index);
        return true;
    }

    /// <summary>Appends every channel's items onto <paramref name="target"/>, channels in
    /// registration order and items in given order. Whatever the target already held leads.</summary>
    public void ComposeOnto(List<T> target)
    {
        foreach (var (_, items) in this._channels) { target.AddRange(items); }
    }

    private int IndexOf(object key)
    {
        for (int i = 0; i < this._channels.Count; i++)
        {
            if (ReferenceEquals(this._channels[i].Key, key)) { return i; }
        }

        return -1;
    }
}
