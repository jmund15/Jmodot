namespace Jmodot.Tools.DocTooltips.DocLookup;

using System;
using System.Collections.Generic;

/// <summary>
/// Builds the .NET XML doc-ID candidates for a member, walking a type's <see cref="Type.BaseType"/>
/// chain most-derived first.
/// </summary>
/// <remarks>
/// Resolving only the runtime type would silently drop every export declared on an ancestor —
/// roughly a third of this codebase's visible export slots, and ~170 concrete classes whose exports
/// are inherited outright. Walking the chain is what recovers them, and it is also why this addon
/// does not key lookups on the attached script's filename the way the community plugin does.
/// </remarks>
public static class DocMemberKey
{
    /// <summary>
    /// Yields candidate doc IDs for <paramref name="memberName"/>, most-derived type first and, for
    /// each type, the property spelling before the field spelling. The first candidate present in
    /// the index wins.
    /// </summary>
    /// <param name="declaringRoot">The runtime type to start from, typically <c>obj.GetType()</c>.</param>
    /// <param name="memberName">CLR member name, e.g. <c>MinOrthoSize</c>.</param>
    public static IEnumerable<string> Resolve(Type declaringRoot, string memberName)
    {
        if (declaringRoot == null || string.IsNullOrEmpty(memberName)) { yield break; }

        for (Type? t = declaringRoot; t != null && t != typeof(object); t = t.BaseType)
        {
            string typeId = DocTypeName(t);
            if (typeId.Length == 0) { continue; }

            yield return $"P:{typeId}.{memberName}";
            yield return $"F:{typeId}.{memberName}";
        }
    }

    /// <summary>
    /// The doc ID of the type itself — its <c>&lt;summary&gt;</c> rather than any member's.
    /// </summary>
    /// <remarks>
    /// Exactly one candidate, never a <see cref="Type.BaseType"/> walk. A member is genuinely
    /// inherited, so resolving an ancestor's text describes the same slot; a class description is
    /// not, and handing a subclass its parent's would mislabel it in a dialog that lists both.
    /// </remarks>
    public static string TypeId(Type type)
    {
        string typeId = DocTypeName(type);
        return typeId.Length == 0 ? string.Empty : $"T:{typeId}";
    }

    /// <summary>
    /// The type's name as the C# compiler spells it in an XML doc ID.
    /// </summary>
    /// <remarks>
    /// Two differences from <see cref="Type.FullName"/>, both verified against an emitted sidecar:
    /// nested types are separated by <c>.</c> rather than reflection's <c>+</c> (no member name in a
    /// 36,441-entry sidecar contained a <c>+</c>), and a generic type keeps its backtick arity
    /// (<c>PoolableProjectileBehavior`1</c>). A constructed generic is reduced to its definition,
    /// because the sidecar documents the definition, not each instantiation.
    /// </remarks>
    public static string DocTypeName(Type type)
    {
        if (type == null) { return string.Empty; }

        Type t = type.IsConstructedGenericType ? type.GetGenericTypeDefinition() : type;
        string? full = t.FullName;
        if (string.IsNullOrEmpty(full))
        {
            // Open generic parameters and similar have no FullName; they never carry doc IDs.
            return string.Empty;
        }

        return full.Replace('+', '.');
    }
}
