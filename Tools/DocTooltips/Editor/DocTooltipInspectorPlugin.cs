#if TOOLS
namespace Jmodot.Tools.DocTooltips.Editor;

using System;
using Godot;
using Jmodot.Tools.DocTooltips.DocLookup;

/// <summary>
/// Paints each <c>[Export]</c>'s <c>&lt;summary&gt;</c> onto its Inspector row as a tooltip.
/// </summary>
/// <remarks>
/// Two engine constraints shape this class.
///
/// First, the text must never land on the <see cref="EditorProperty"/> row itself.
/// <c>EditorInspector::update_tree()</c> stamps every row with a doc-ID key
/// (<c>"property|&lt;Class&gt;|&lt;prop&gt;"</c>) and sets a private <c>has_doc_tooltip</c> flag; on hover a
/// flagged row goes through <c>EditorHelpBit::parse_symbol</c>, which <c>ERR_FAIL</c>s on any string
/// with fewer than three pipe-separated slices. Prose on the row is an error path, not a tooltip.
/// Godot resolves a tooltip by walking UP from the control under the cursor, and a descendant
/// carries neither the flag nor a <c>make_custom_tooltip</c> override — so prose renders normally
/// there. <see cref="PaintDescendants"/> therefore starts at the row's CHILDREN.
///
/// Second, the rows do not exist when <c>_ParseProperty</c> runs — the built-in editors are built
/// afterwards. The apply pass is deferred twice; one frame is not enough for the rows to be inside
/// the tree. Both hops defer through <see cref="GodotObject.CallDeferred(StringName, Variant[])"/>
/// on this object rather than a <see cref="Callable"/> over a lambda — see
/// <see cref="DocTooltipInstallation"/> for why a delegate-backed callable crashes the editor.
///
/// Summaries are resolved at apply time from each row's own <see cref="EditorProperty.GetEditedObject"/>
/// rather than from a name map built during parsing. An inlined sub-resource is edited by its own
/// rows, so per-row resolution gives nested Resources the correct type for free and makes a name
/// collision between an object and its sub-resource impossible.
/// </remarks>
[Tool]
public partial class DocTooltipInspectorPlugin : EditorInspectorPlugin
{
    private readonly DocSummaryResolver _resolver = null!;

    // Set while a coalesced apply pass is pending, so N _ParseEnd firings schedule ONE pass.
    private bool _applyScheduled;

    /// <summary>
    /// Required by the engine, never used by this addon.
    /// </summary>
    /// <remarks>
    /// Godot recreates a managed instance for every script-bearing object when it reloads the
    /// assembly, and it does so through <c>ScriptManagerBridge</c>, which can only call a
    /// PARAMETERLESS constructor. A <see cref="GodotObject"/>-derived script class without one
    /// throws <c>MissingMemberException</c> on that path and takes the editor down with it — the
    /// crash is a native fault during reload, so nothing points back here. Every type in this
    /// folder therefore keeps a parameterless constructor, whatever its real construction path.
    /// The instance it produces is inert: <see cref="_resolver"/> stays null, and the engine
    /// discards it.
    /// </remarks>
    public DocTooltipInspectorPlugin()
    {
    }

    public DocTooltipInspectorPlugin(DocSummaryResolver resolver)
    {
        this._resolver = resolver;
    }

    public override bool _CanHandle(GodotObject @object) => true;

    public override bool _ParseProperty(
        GodotObject @object,
        Variant.Type type,
        string name,
        PropertyHint hintType,
        string hintString,
        PropertyUsageFlags usageFlags,
        bool wide)
        // false keeps the built-in editor; true would REMOVE it. This addon decorates, never replaces.
        => false;

    public override void _ParseEnd(GodotObject @object)
    {
        // The engine's reload-recreated instance (see the parameterless constructor) carries no
        // resolver, yet stays registered in the inspector's plugin list and keeps receiving this
        // call — so this is a hot path, not a defensive nicety, and must lead the method.
        if (this._resolver == null) { return; }

        // One stat per parsed object, never one per property lookup — TryGetSummary never stats.
        this._resolver.Refresh();

        // _ParseEnd fires once per parsed object — the edited object plus every inlined sub-resource,
        // since _CanHandle accepts all. Each Apply walks whole inspector trees, so scheduling one per
        // firing is O(N^2) row visits for N objects. The pass is idempotent and resolves every row
        // from its own edited object, so one pass after the last _ParseEnd paints exactly the same
        // result as N passes.
        if (this._applyScheduled) { return; }

        this._applyScheduled = true;
        this.CallDeferred(MethodName.DeferApplyPass);
    }

    /// <summary>
    /// Second hop of the two-frame delay described above. Addressed by the engine through this
    /// object's ID, so it must stay public and instance-bound; never call it directly.
    /// </summary>
    public void DeferApplyPass() => this.CallDeferred(MethodName.RunApplyPass);

    /// <summary>Runs the coalesced pass and reopens scheduling. Public for the same reason.</summary>
    public void RunApplyPass()
    {
        this._applyScheduled = false;
        Apply(this._resolver);
    }

    /// <summary>
    /// Paints every live <see cref="EditorInspector"/>, not just the main dock's. A plugin, a
    /// secondary dock, or a dialog can host its own inspector; those rows fire
    /// <see cref="_ParseEnd"/> like any others, so painting only
    /// <c>EditorInterface.GetInspector()</c> would leave them permanently untouched.
    /// </summary>
    private static void Apply(DocSummaryResolver resolver)
    {
        Control? editorRoot = EditorInterface.Singleton?.GetBaseControl();
        if (editorRoot == null || !GodotObject.IsInstanceValid(editorRoot)) { return; }

        WalkInspectors(editorRoot, resolver);
    }

    private static void WalkInspectors(Node node, DocSummaryResolver resolver)
    {
        if (!GodotObject.IsInstanceValid(node)) { return; }

        // An EditorInspector never nests inside another, so this stops descending once one is found
        // and hands the subtree to Walk. That keeps the editor-wide scan to a single traversal.
        if (node is EditorInspector inspector)
        {
            Walk(inspector, resolver);
            return;
        }

        foreach (Node child in node.GetChildren())
        {
            WalkInspectors(child, resolver);
        }
    }

    private static void Walk(Node node, DocSummaryResolver resolver)
    {
        if (node is EditorProperty row)
        {
            TryPaintRow(row, resolver);
        }

        foreach (Node child in node.GetChildren())
        {
            Walk(child, resolver);
        }
    }

    private static void TryPaintRow(EditorProperty row, DocSummaryResolver resolver)
    {
        // An object can be freed between the two deferral hops. A freed Godot node is NOT null in
        // C# -- the managed wrapper survives while the native object is gone -- so `?.` and null
        // checks pass while the access still faults. IsInstanceValid is the only correct guard.
        if (!GodotObject.IsInstanceValid(row) || !row.IsInsideTree()) { return; }

        GodotObject edited = row.GetEditedObject();
        if (edited == null || !GodotObject.IsInstanceValid(edited)) { return; }

        string property = row.GetEditedProperty().ToString();
        if (property.Length == 0) { return; }

        Type editedType = edited.GetType();
        if (resolver.TryGetSummaryForMember(editedType, property, out string? summary))
        {
            PaintDescendants(row, DocTooltipText.Compose(editedType, property, summary));
        }
    }

    /// <summary>
    /// Sets <see cref="Control.TooltipText"/> on every Control BELOW <paramref name="row"/> — never
    /// on the row itself, which is the engine's doc-ID error path (see the class remarks).
    /// </summary>
    private static void PaintDescendants(Node row, string summary)
    {
        foreach (Node child in row.GetChildren())
        {
            PaintSubtree(child, summary);
        }
    }

    private static void PaintSubtree(Node node, string summary)
    {
        if (!GodotObject.IsInstanceValid(node)) { return; }

        // Stop at a nested row. An inlined sub-resource's rows are DESCENDANTS of the row that owns
        // it, and Walk paints each from its own edited object. Descending past this boundary would
        // leave the outer property's text on every nested export that documents nothing — a tooltip
        // naming the wrong member, which is worse than no tooltip.
        if (node is EditorProperty) { return; }

        if (node is Control control)
        {
            control.TooltipText = summary;
        }

        foreach (Node child in node.GetChildren())
        {
            PaintSubtree(child, summary);
        }
    }
}
#endif
