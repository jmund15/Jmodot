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
internal sealed class MatchListTooltipSurface : ICreateDialogDocSurface
{
    private readonly ClassSummaryLookup _lookup;
    private CreateDialogParts? _parts;

    internal MatchListTooltipSurface(ClassSummaryLookup lookup)
    {
        this._lookup = lookup;
    }

    public bool TryAttach(CreateDialogParts parts)
    {
        this._parts = parts;
        parts.Dialog.AboutToPopup += this.OnAboutToPopup;
        parts.Search.TextChanged += this.OnSearchTextChanged;
        return true;
    }

    public void Detach()
    {
        CreateDialogParts? parts = this._parts;
        this._parts = null;
        if (parts == null) { return; }

        if (GodotObject.IsInstanceValid(parts.Dialog)) { parts.Dialog.AboutToPopup -= this.OnAboutToPopup; }
        if (GodotObject.IsInstanceValid(parts.Search)) { parts.Search.TextChanged -= this.OnSearchTextChanged; }
    }

    private void OnAboutToPopup() => this.ScheduleRepaint();

    private void OnSearchTextChanged(string _) => this.ScheduleRepaint();

    // Deferred so the pass always runs after the engine has finished rebuilding the tree, whatever
    // order the handlers for a given signal happen to fire in. A tooltip needs a held hover to
    // appear, so a frame of latency is not observable.
    private void ScheduleRepaint() => Callable.From(this.Repaint).CallDeferred();

    private void Repaint()
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
