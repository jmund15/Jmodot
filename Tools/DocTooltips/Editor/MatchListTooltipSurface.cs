#if TOOLS
namespace Jmodot.Tools.DocTooltips.Editor;

using Godot;
using Jmodot.Tools.DocTooltips.DocLookup;

/// <summary>
/// Surface A — paints each Matches row's hover tooltip with its class summary.
/// </summary>
/// <remarks>
/// Rests entirely on public API: <see cref="TreeItem.SetTooltipText"/> on rows the engine itself
/// leaves blank for C# (it fills them from the doc database, which C# never populates). That is what
/// makes this the fallback when <see cref="DescriptionPanelSurface"/> cannot find its widget — no
/// part of it depends on editor-internal structure.
///
/// Repaints on a whole-tree pass rather than per row, because the engine REBUILDS the tree on every
/// search keystroke; there is no per-row hook to hang a tooltip on that would survive that.
/// </remarks>
[Tool]
internal sealed partial class MatchListTooltipSurface : GodotObject, ICreateDialogDocSurface
{
    private readonly ClassSummaryLookup _lookup = null!;
    private CreateDialogParts? _parts;

    /// <summary>
    /// Required by the engine's reload path, never used by this addon — see
    /// <see cref="DocTooltipInspectorPlugin()"/> for why omitting it crashes the editor.
    /// </summary>
    public MatchListTooltipSurface()
    {
    }

    internal MatchListTooltipSurface(ClassSummaryLookup lookup)
    {
        this._lookup = lookup;
    }

    public bool TryAttach(CreateDialogParts parts)
    {
        this._parts = parts;
        parts.Dialog.Connect(Window.SignalName.AboutToPopup, this.PopupWatch());
        parts.Search.Connect(LineEdit.SignalName.TextChanged, this.SearchWatch());
        return true;
    }

    public void Detach()
    {
        CreateDialogParts? parts = this._parts;
        this._parts = null;
        if (parts == null) { return; }

        if (GodotObject.IsInstanceValid(parts.Dialog))
        {
            parts.Dialog.Disconnect(Window.SignalName.AboutToPopup, this.PopupWatch());
        }

        if (GodotObject.IsInstanceValid(parts.Search))
        {
            parts.Search.Disconnect(LineEdit.SignalName.TextChanged, this.SearchWatch());
        }
    }

    // ObjectID-bound per the folder invariant (see DocTooltipInstallation): these emitters are
    // editor-lifetime, so a delegate-backed connection outlives this assembly and faults on dispatch.
    private Callable PopupWatch() => new(this, MethodName.ScheduleRepaint);

    private Callable SearchWatch() => new(this, MethodName.OnSearchTextChanged);

    /// <summary>
    /// Discards the search text the signal carries and repaints. Public for ObjectID addressing;
    /// never call it directly.
    /// </summary>
    public void OnSearchTextChanged(string _) => this.ScheduleRepaint();

    /// <summary>
    /// Queues the repaint. Public for ObjectID addressing; never call it directly.
    /// </summary>
    /// <remarks>
    /// Deferred so the pass always runs after the engine has finished rebuilding the tree, whatever
    /// order the handlers for a given signal happen to fire in. A tooltip needs a held hover to
    /// appear, so a frame of latency is not observable.
    /// </remarks>
    public void ScheduleRepaint() => this.CallDeferred(MethodName.Repaint);

    /// <summary>Repaints every row's tooltip. Public for ObjectID addressing.</summary>
    public void Repaint()
    {
        CreateDialogParts? parts = this._parts;
        if (parts == null || !GodotObject.IsInstanceValid(parts.Matches)) { return; }

        // One stat for the whole pass, never one per row.
        this._lookup.Refresh();

        for (TreeItem? item = parts.Matches.GetRoot(); item != null; item = item.GetNextInTree())
        {
            string className = CreateDialogParts.ClassNameOf(item);
            if (this._lookup.TryGetSummary(className, out string? summary))
            {
                item.SetTooltipText(0, DocTooltipText.ComposeClass(className, summary));
            }
        }
    }
}
#endif
