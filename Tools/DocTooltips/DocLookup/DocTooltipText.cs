namespace Jmodot.Tools.DocTooltips.DocLookup;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

/// <summary>
/// Renders a resolved summary as the finished tooltip string: a header naming the property and its
/// type, a blank line, then the summary wrapped to a bounded width.
/// </summary>
/// <remarks>
/// Both halves exist because <c>Control.TooltipText</c> is a plain string and the engine's default
/// tooltip renders it in a minimum-sized <c>Label</c> with autowrap off. The line breaks therefore
/// have to be IN the string — nothing downstream inserts them — and the wrap is by character count,
/// which is approximate under the proportional editor font. That buys a readable block, not
/// typographic justification; a custom tooltip control, which could measure properly, is only
/// reachable by overriding <c>_MakeCustomTooltip</c> on the control that supplies the text, and that
/// control is a built-in Inspector widget this addon does not own.
///
/// The header restores what the engine shows for a native property ("Property slide_on_ceiling:
/// bool"). Godot renders that header for C# properties too, but only on the row's LABEL, where the
/// description is always empty; this addon's text lands on the value widget, so without a header the
/// two halves of one row disagree about which member is being described.
///
/// Pure and Godot-free like the rest of this namespace, which is what keeps it unit-testable.
/// </remarks>
public static class DocTooltipText
{
    /// <summary>
    /// Longest line the wrap will emit, in characters. Near the measure of the engine's own help
    /// popups; a single word longer than this overflows rather than being split.
    /// </summary>
    public const int WrapColumn = 90;

    private static readonly Dictionary<Type, string> Aliases = new()
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(string)] = "string",
        [typeof(object)] = "object",
    };

    /// <summary>
    /// Composes the tooltip for <paramref name="memberName"/> as declared on
    /// <paramref name="declaringRoot"/> or any ancestor.
    /// </summary>
    /// <param name="declaringRoot">Runtime type of the edited object, typically <c>obj.GetType()</c>.</param>
    /// <param name="memberName">CLR member name, as the Inspector row reports it.</param>
    /// <param name="summary">
    /// The resolved summary, already collapsed to a single whitespace-normalised line by
    /// <see cref="XmlDocIndex"/>.
    /// </param>
    public static string Compose(Type declaringRoot, string memberName, string summary)
    {
        string header = Header(declaringRoot, memberName);
        string body = Wrap(summary);

        if (header.Length == 0) { return body; }

        return body.Length == 0 ? header : $"{header}\n\n{body}";
    }

    /// <summary>
    /// Composes the tooltip for a class rather than a member — the same shape with the noun Godot's
    /// own class help uses ("Class Node").
    /// </summary>
    /// <param name="className">The name the class is registered under, as the surface displays it.</param>
    /// <param name="summary">The resolved class summary, already collapsed to a single line.</param>
    public static string ComposeClass(string className, string summary)
    {
        string body = Wrap(summary);
        if (string.IsNullOrEmpty(className)) { return body; }

        string header = $"Class {className}";
        return body.Length == 0 ? header : $"{header}\n\n{body}";
    }

    private static string Header(Type declaringRoot, string memberName)
    {
        if (string.IsNullOrEmpty(memberName)) { return string.Empty; }

        // "Property" regardless of the CLR spelling behind it: that is the Inspector's own word for
        // every row, and an export can be a field.
        Type? memberType = MemberType(declaringRoot, memberName);
        return memberType == null
            ? $"Property {memberName}"
            : $"Property {memberName}: {TypeLabel(memberType)}";
    }

    /// <summary>
    /// The member's declared type, walking the <see cref="Type.BaseType"/> chain most-derived first
    /// and property spelling before field — the same resolution order
    /// <see cref="DocMemberKey.Resolve"/> uses, so the header names the type of the very member whose
    /// summary was found.
    /// </summary>
    /// <remarks>
    /// <c>DeclaredOnly</c> plus an explicit walk rather than reflection's own inheritance search:
    /// Godot exports private fields, and inherited PRIVATE members are invisible to a flattened
    /// lookup at any binding flags.
    /// </remarks>
    private static Type? MemberType(Type declaringRoot, string memberName)
    {
        if (declaringRoot == null) { return null; }

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        for (Type? t = declaringRoot; t != null && t != typeof(object); t = t.BaseType)
        {
            PropertyInfo? property = t.GetProperty(memberName, Flags);
            if (property != null) { return property.PropertyType; }

            FieldInfo? field = t.GetField(memberName, Flags);
            if (field != null) { return field.FieldType; }
        }

        return null;
    }

    /// <summary>The type as C# spells it: aliases for primitives, angle brackets for generic arguments.</summary>
    private static string TypeLabel(Type type)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) { return TypeLabel(underlying) + "?"; }

        if (type.IsArray) { return TypeLabel(type.GetElementType()!) + "[]"; }

        if (Aliases.TryGetValue(type, out string? alias)) { return alias; }

        if (!type.IsGenericType) { return type.Name; }

        string name = type.Name;
        int arity = name.IndexOf('`');
        if (arity >= 0) { name = name[..arity]; }

        var sb = new StringBuilder(name).Append('<');
        Type[] arguments = type.GetGenericArguments();
        for (int i = 0; i < arguments.Length; i++)
        {
            if (i > 0) { sb.Append(", "); }
            sb.Append(TypeLabel(arguments[i]));
        }

        return sb.Append('>').ToString();
    }

    /// <summary>
    /// Breaks <paramref name="text"/> on spaces so no line exceeds <see cref="WrapColumn"/>. A word
    /// longer than the column gets its own overflowing line rather than being split — summaries name
    /// identifiers and paths, and a break inside one would read as a different token.
    /// </summary>
    private static string Wrap(string text)
    {
        var sb = new StringBuilder(text.Length + (text.Length / WrapColumn) + 1);
        int lineLength = 0;

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (lineLength > 0 && lineLength + 1 + word.Length > WrapColumn)
            {
                sb.Append('\n');
                lineLength = 0;
            }
            else if (lineLength > 0)
            {
                sb.Append(' ');
                lineLength++;
            }

            sb.Append(word);
            lineLength += word.Length;
        }

        return sb.ToString();
    }
}
