namespace Jmodot.Tools.DocTooltips.DocLookup;

using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Answers "what is this member's documented summary?" for a runtime type and member name.
/// </summary>
/// <remarks>
/// This is the single public seam of the doc-tooltip data layer. It owns the resolution policy —
/// walk the <see cref="Type.BaseType"/> chain most-derived first, property spelling before field,
/// first match wins — so no caller re-implements it. <see cref="DocMemberKey"/> and
/// <see cref="XmlDocIndex"/> are its internals; handing callers a candidate list instead would put
/// the same loop at every call site.
///
/// Pure and Godot-free by construction, which is what keeps it testable without an engine runtime.
/// </remarks>
public sealed class DocSummaryResolver
{
    private readonly XmlDocIndex _index;

    /// <param name="index">The sidecar index to resolve against.</param>
    public DocSummaryResolver(XmlDocIndex index)
    {
        this._index = index;
    }

    /// <summary>
    /// Reloads the backing sidecar if its timestamp moved. Call once per batch of lookups — the
    /// lookup path itself never touches the filesystem.
    /// </summary>
    /// <returns><c>true</c> if the index was rebuilt by this call.</returns>
    public bool Refresh() => this._index.RefreshIfStale();

    /// <summary>Number of members currently loaded. Zero means nothing resolvable.</summary>
    public int LoadedCount => this._index.Count;

    /// <summary>Absolute path of the sidecar being resolved against.</summary>
    public string SidecarPath => this._index.Path;

    /// <summary>
    /// Resolves the summary for <paramref name="memberName"/> as declared on
    /// <paramref name="declaringRoot"/> or any ancestor.
    /// </summary>
    /// <param name="declaringRoot">Runtime type of the edited object, typically <c>obj.GetType()</c>.</param>
    /// <param name="memberName">CLR member name.</param>
    /// <param name="summary">Single-line summary text when found.</param>
    /// <returns><c>false</c> when no ancestor documents the member.</returns>
    public bool TryGetSummaryForMember(
        Type declaringRoot,
        string memberName,
        [MaybeNullWhen(false)] out string summary)
    {
        foreach (string docId in DocMemberKey.Resolve(declaringRoot, memberName))
        {
            if (this._index.TryGetSummary(docId, out string? found))
            {
                summary = found;
                return true;
            }
        }

        summary = null;
        return false;
    }

    /// <summary>
    /// Resolves the summary documenting <paramref name="type"/> itself.
    /// </summary>
    /// <param name="type">The runtime type to describe.</param>
    /// <param name="summary">Single-line summary text when found.</param>
    /// <returns><c>false</c> when the type carries no <c>&lt;summary&gt;</c> of its own.</returns>
    public bool TryGetSummaryForType(Type type, [MaybeNullWhen(false)] out string summary)
    {
        string docId = DocMemberKey.TypeId(type);
        if (docId.Length > 0 && this._index.TryGetSummary(docId, out string? found))
        {
            summary = found;
            return true;
        }

        summary = null;
        return false;
    }
}
