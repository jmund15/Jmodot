#if TOOLS
namespace Jmodot.Tools.DocTooltips.Editor;

using Godot;
using Jmodot.Tools.DocTooltips.DocLookup;

/// <summary>
/// Surface B — fills the dialog's own "Description:" panel, where the engine shows
/// "No description available." for every C# class.
/// </summary>
/// <remarks>
/// This is the surface that looks right and the one that can break. The engine's own API for exactly
/// this — <c>EditorHelpBit::set_custom_text()</c>, which it uses to print "No results for X" into
/// this very panel — is unreachable: <c>EditorHelpBit::_bind_methods()</c> registers one signal and
/// ZERO methods, so no <c>Call()</c> can reach it. What remains is writing into the panel's
/// <c>content</c> <see cref="RichTextLabel"/>, which IS a public type, located structurally by
/// <see cref="CreateDialogParts"/>.
///
/// Three consequences a maintainer needs, all verified against 4.7.1-stable source:
///
/// The write must happen AFTER the engine's own. Selecting a row runs
/// <c>CreateDialog::_item_selected</c> → <c>parse_symbol()</c> → <c>_update_labels()</c>
/// synchronously from the Tree's <c>cell_selected</c> signal. Connecting to that same signal puts
/// this handler later in the same invocation list, so it overwrites rather than races.
///
/// It is NOT permanent. <c>_update_labels()</c> also runs on <c>NOTIFICATION_THEME_CHANGED</c> and
/// rebuilds the label from the engine's own <c>help_data</c>, discarding this text. Changing the
/// editor theme therefore blanks the description back to "No description available." until the next
/// row selection repaints it.
///
/// A miss leaves the panel ALONE. Every C++ class in that list has a real engine description, so
/// clearing on a failed lookup would destroy correct text — this only ever overwrites when it has
/// something to say. That asymmetry is load-bearing, not defensive coding.
///
/// Text goes in through <see cref="RichTextLabel.AddText"/>, never the <c>Text</c> property: the
/// label parses BBCode, and a summary naming <c>[Export]</c> or <c>[Tool]</c> would be silently
/// eaten as markup. No manual wrapping either — unlike the plain Label behind a tooltip, this label
/// autowraps.
/// </remarks>
internal sealed class DescriptionPanelSurface : ICreateDialogDocSurface
{
    private readonly ClassSummaryLookup _lookup;
    private CreateDialogParts? _parts;

    internal DescriptionPanelSurface(ClassSummaryLookup lookup)
    {
        this._lookup = lookup;
    }

    public bool TryAttach(CreateDialogParts parts)
    {
        if (parts.Description == null) { return false; }

        this._parts = parts;
        parts.Matches.CellSelected += this.OnCellSelected;
        parts.Dialog.AboutToPopup += this.OnAboutToPopup;
        return true;
    }

    public void Detach()
    {
        CreateDialogParts? parts = this._parts;
        this._parts = null;
        if (parts == null) { return; }

        if (GodotObject.IsInstanceValid(parts.Matches)) { parts.Matches.CellSelected -= this.OnCellSelected; }
        if (GodotObject.IsInstanceValid(parts.Dialog)) { parts.Dialog.AboutToPopup -= this.OnAboutToPopup; }
    }

    // Immediate: the engine's handler on this same signal has already run and written the panel.
    private void OnCellSelected() => this.Apply();

    // Deferred: the dialog restores its previous selection while popping up, and that path does not
    // necessarily re-emit cell_selected.
    private void OnAboutToPopup() => Callable.From(this.Apply).CallDeferred();

    private void Apply()
    {
        CreateDialogParts? parts = this._parts;
        if (parts?.Description == null
            || !GodotObject.IsInstanceValid(parts.Description)
            || !GodotObject.IsInstanceValid(parts.Matches))
        {
            return;
        }

        TreeItem? selected = parts.Matches.GetSelected();
        if (selected == null) { return; }

        this._lookup.Refresh();
        if (!this._lookup.TryGetSummary(CreateDialogParts.ClassNameOf(selected), out string? summary))
        {
            return;
        }

        parts.Description.Clear();
        parts.Description.AddText(summary);
    }
}
#endif
