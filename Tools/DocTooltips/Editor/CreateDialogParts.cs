#if TOOLS
namespace Jmodot.Tools.DocTooltips.Editor;

using Godot;

/// <summary>
/// The widgets inside one Create-New-Node dialog that a doc surface writes to, located by structure.
/// </summary>
/// <remarks>
/// <c>CreateDialog</c> has no script binding, so nothing here can be reached by type — the node's
/// <see cref="GodotObject.GetClass"/> string and the surrounding structure are the only handles.
/// Three engine details shape the location rules, all verified against <c>create_dialog.cpp</c> at
/// 4.7.1-stable.
///
/// The dialog holds TWO <see cref="Tree"/>s — Favorites and Matches — and Favorites is constructed
/// first, so "the first Tree in the walk" reliably finds the WRONG one. Matches is instead located
/// from the search box: the nearest ancestor of the search box owning a Tree as a direct child.
/// Favorites sits on the other side of the split and is never reached that way.
///
/// The description panel is an <c>EditorHelpBit</c> holding two <see cref="RichTextLabel"/>s,
/// <c>title</c> then <c>content</c>. Content is identified by its vertical Expand flag — the
/// property that makes it the growing label — rather than by child index, so a label inserted
/// between them would not silently redirect the write to the title.
///
/// A missing description panel is reported as <c>null</c>, never as a failure to locate: the
/// match-list surface does not need it, and only the panel surface treats its absence as fatal.
/// </remarks>
internal sealed class CreateDialogParts
{
    private CreateDialogParts(Window dialog, Tree matches, LineEdit search, RichTextLabel? description)
    {
        this.Dialog = dialog;
        this.Matches = matches;
        this.Search = search;
        this.Description = description;
    }

    /// <summary>The dialog itself, for its <c>AboutToPopup</c> signal.</summary>
    internal Window Dialog { get; }

    /// <summary>The Matches list — NOT the Favorites list beside it.</summary>
    internal Tree Matches { get; }

    /// <summary>The search box, whose text changes rebuild <see cref="Matches"/>.</summary>
    internal LineEdit Search { get; }

    /// <summary>The description panel's content label, or <c>null</c> when it cannot be located.</summary>
    internal RichTextLabel? Description { get; }

    /// <summary>
    /// Locates the parts of <paramref name="dialog"/>, or returns <c>false</c> when its structure no
    /// longer matches — the signal that an engine update moved something.
    /// </summary>
    internal static bool TryLocate(Node dialog, out CreateDialogParts parts)
    {
        parts = null!;

        if (dialog is not Window window) { return false; }

        LineEdit? search = FirstDescendant<LineEdit>(dialog);
        if (search == null) { return false; }

        Tree? matches = MatchesTreeAbove(search, dialog);
        if (matches == null) { return false; }

        parts = new CreateDialogParts(window, matches, search, DescriptionContent(dialog));
        return true;
    }

    /// <summary>
    /// The class a row stands for, derived exactly as <c>CreateDialog::get_selected_type_name()</c>
    /// does it: the row's text up to the first space.
    /// </summary>
    /// <remarks>
    /// The text is not always the bare name — a keyword match appends "      - Matches the ..." — and
    /// mirroring the engine's own slice is what keeps this reading the same name the dialog will
    /// actually instantiate.
    /// </remarks>
    internal static string ClassNameOf(TreeItem item)
    {
        string text = item.GetText(0);
        int space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }

    /// <summary>
    /// Every live <c>CreateDialog</c> under <paramref name="root"/>. Class-name matched because the
    /// type is unbound; one instance exists per dock that can create nodes or resources.
    /// </summary>
    internal static void CollectDialogs(Node root, System.Collections.Generic.List<Node> into)
    {
        if (!GodotObject.IsInstanceValid(root)) { return; }

        if (root.GetClass() == "CreateDialog")
        {
            // A CreateDialog never nests inside another, so this subtree needs no further walking.
            into.Add(root);
            return;
        }

        foreach (Node child in root.GetChildren())
        {
            CollectDialogs(child, into);
        }
    }

    private static Tree? MatchesTreeAbove(Node search, Node stopAt)
    {
        for (Node? node = search.GetParent(); node != null; node = node.GetParent())
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is Tree tree) { return tree; }
            }

            if (node.GetInstanceId() == stopAt.GetInstanceId()) { break; }
        }

        return null;
    }

    private static RichTextLabel? DescriptionContent(Node dialog)
    {
        Node? helpBit = FirstDescendantOfClass(dialog, "EditorHelpBit");
        if (helpBit == null) { return null; }

        foreach (Node child in helpBit.GetChildren())
        {
            if (child is RichTextLabel label && label.SizeFlagsVertical.HasFlag(Control.SizeFlags.Expand))
            {
                return label;
            }
        }

        return null;
    }

    private static T? FirstDescendant<T>(Node root) where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T match) { return match; }

            T? found = FirstDescendant<T>(child);
            if (found != null) { return found; }
        }

        return null;
    }

    private static Node? FirstDescendantOfClass(Node root, string className)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child.GetClass() == className) { return child; }

            Node? found = FirstDescendantOfClass(child, className);
            if (found != null) { return found; }
        }

        return null;
    }
}
#endif
