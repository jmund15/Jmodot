namespace Jmodot.Tools.DocTooltips.DocLookup;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Answers "what is this class?" from the bare name a Godot editor surface knows it by.
/// </summary>
/// <remarks>
/// The Create-New-Node dialog names a class the way Godot registers it — <c>FloorCellActivator</c> —
/// while the XML sidecar keys on the namespace-qualified spelling. This owns that bridge so each
/// display surface consumes one call instead of re-deriving the mapping.
///
/// Which types count as registered is the CALLER's decision: it passes the sequence, which keeps
/// this type free of any dependency on Godot's attributes and therefore unit-testable without an
/// engine runtime, like the rest of this namespace.
/// </remarks>
public sealed class ClassSummaryLookup
{
    private readonly DocSummaryResolver _resolver;
    private readonly Dictionary<string, Type> _byClassName = new(StringComparer.Ordinal);

    /// <param name="resolver">The doc-summary seam to resolve against.</param>
    /// <param name="registeredClasses">
    /// The types the editor surface can name, typically the assembly's <c>[GlobalClass]</c> types.
    /// </param>
    public ClassSummaryLookup(DocSummaryResolver resolver, IEnumerable<Type> registeredClasses)
    {
        this._resolver = resolver;

        foreach (Type type in registeredClasses)
        {
            // First wins, and a repeat is never an error. Godot rejects duplicate global class names
            // itself, so a collision here is a caller passing an unfiltered sequence — and throwing
            // would abort the whole plugin registration over a name the dialog cannot even show.
            if (type != null) { this._byClassName.TryAdd(type.Name, type); }
        }
    }

    /// <summary>Number of class names this can resolve a type for.</summary>
    public int RegisteredCount => this._byClassName.Count;

    /// <summary>
    /// Reloads the backing sidecar if its timestamp moved. Call once per batch of lookups — the
    /// lookup path itself never touches the filesystem.
    /// </summary>
    /// <returns><c>true</c> if the index was rebuilt by this call.</returns>
    public bool Refresh() => this._resolver.Refresh();

    /// <summary>
    /// Resolves the summary documenting the class registered as <paramref name="className"/>.
    /// </summary>
    /// <returns>
    /// <c>false</c> when no registered type carries that name, or when the type documents nothing.
    /// </returns>
    public bool TryGetSummary(string className, [MaybeNullWhen(false)] out string summary)
    {
        if (!string.IsNullOrEmpty(className) && this._byClassName.TryGetValue(className, out Type? type))
        {
            return this._resolver.TryGetSummaryForType(type, out summary);
        }

        summary = null;
        return false;
    }
}
