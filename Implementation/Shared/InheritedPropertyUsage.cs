namespace Jmodot.Implementation.Shared;

using System.Collections.Generic;
using Godot;

/// <summary>Inspector and storage classification for a node's inherited native properties, for <c>_ValidateProperty</c>.</summary>
public static class InheritedPropertyUsage
{
    /// <summary>A <paramref name="derived"/> name is shown read-only and never saved; an <paramref name="inert"/> name is
    /// hidden and never saved. Returns true when <paramref name="property"/> matched either set.</summary>
    public static bool Apply(Godot.Collections.Dictionary property, IReadOnlySet<StringName> derived, IReadOnlySet<StringName> inert)
    {
        var name = property["name"].AsStringName();
        var usage = (PropertyUsageFlags)property["usage"].AsInt64();
        if (derived.Contains(name))
        {
            property["usage"] = (long)((usage & ~PropertyUsageFlags.Storage) | PropertyUsageFlags.ReadOnly);
            return true;
        }
        if (inert.Contains(name))
        {
            property["usage"] = (long)(usage & ~(PropertyUsageFlags.Editor | PropertyUsageFlags.Storage));
            return true;
        }
        return false;
    }
}
