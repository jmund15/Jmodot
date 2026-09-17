namespace Jmodot.Core.Stats;

using Godot.Collections;

/// <summary>
/// Query helpers over an authored <see cref="StatModifier" /> array.
/// </summary>
public static class StatModifierExtensions
{
    /// <summary>
    /// True when any entry targets <paramref name="attribute" /> by reference. Null elements
    /// (an unauthored or stripped slot) are skipped rather than thrown on.
    /// </summary>
    public static bool Targets(this Array<StatModifier> entries, Attribute attribute)
    {
        foreach (var entry in entries)
        {
            if (entry?.Attribute == attribute) { return true; }
        }

        return false;
    }
}
