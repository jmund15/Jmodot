namespace Jmodot.Core.Stats;

using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Godot.Collections;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
/// Query and validation helpers over an authored <see cref="StatModifier" /> sequence.
/// </summary>
public static class StatModifierExtensions
{
    /// <summary>
    /// True when any entry targets <paramref name="attribute" /> by reference. Null elements
    /// (an unauthored or stripped slot) are skipped rather than thrown on.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="entries" /> or <paramref name="attribute" /> is null.</exception>
    public static bool Targets(this Array<StatModifier> entries, Attribute attribute)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(attribute);

        foreach (var entry in entries)
        {
            if (entry?.Attribute == attribute) { return true; }
        }

        return false;
    }

    /// <summary>
    /// Validates an authored <paramref name="entries" /> array before any element is applied: a null
    /// element and an element missing a required export both fail loud, naming <paramref name="holder" />
    /// and the failing index. Every consumer of an authored modifier array goes through this one sweep,
    /// so the message and the index cannot drift between holder families.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="entries" /> or <paramref name="holder" /> is null.</exception>
    /// <exception cref="ResourceConfigurationException">An element is null, or is missing a required export.</exception>
    public static void ValidateEntries(this IReadOnlyList<StatModifier> entries, Resource holder)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ValidateEntriesCore(entries, holder, holder);
    }

    /// <summary>
    /// <see cref="ValidateEntries(IReadOnlyList{StatModifier}, Resource)" /> for a copied or derived array
    /// with no authored holder Resource in scope — <paramref name="context" /> labels the owning caller in
    /// the log instead, and the failure keeps the <see cref="InvalidDataException" /> those sites throw.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="entries" /> or <paramref name="context" /> is null.</exception>
    /// <exception cref="InvalidDataException">An element is null, or is missing a required export.</exception>
    public static void ValidateEntries(this IReadOnlyList<StatModifier> entries, object context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateEntriesCore(entries, context, holder: null);
    }

    private static void ValidateEntriesCore(
        IReadOnlyList<StatModifier> entries, object context, Resource? holder)
    {
        ArgumentNullException.ThrowIfNull(entries);

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry is null)
            {
                string message = $"Unauthored (null) StatModifier element at index {index}.";
                throw JmoLogger.LogAndRethrow(
                    holder is null
                        ? new InvalidDataException(message)
                        : new ResourceConfigurationException($"{message} Holder: '{holder.ResourcePath}'.", holder),
                    context);
            }

            entry.ValidateRequiredExports();
        }
    }
}
