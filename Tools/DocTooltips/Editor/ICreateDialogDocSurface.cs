#if TOOLS
namespace Jmodot.Tools.DocTooltips.Editor;

/// <summary>
/// One way of putting a C# class summary in front of the author inside the Create-New-Node dialog.
/// </summary>
/// <remarks>
/// Two exist because they trade differently, not because either is a step toward the other:
/// <see cref="MatchListTooltipSurface"/> writes row tooltips through public <see cref="Godot.Tree"/>
/// API, while <see cref="DescriptionPanelSurface"/> fills the dialog's own Description panel by
/// writing into an editor-internal widget. Which one runs is authored, not inferred — see
/// <see cref="CreateDialogDocs.SettingPath"/>.
///
/// Contract:
/// <list type="bullet">
/// <item>One instance serves ONE dialog. <see cref="TryAttach"/> is called at most once per instance.</item>
/// <item><see cref="TryAttach"/> returning <c>false</c> means this dialog does not expose what the
/// surface needs and NOTHING was connected — the caller is free to attach a different surface to the
/// same parts. It is not an error and must not be reported as one by the implementation; the caller
/// owns that decision because only it knows whether a fallback exists.</item>
/// <item><see cref="Detach"/> must be safe to call after a failed <see cref="TryAttach"/>, twice, and
/// after the dialog's nodes have been freed. It is the only teardown hook; the editor reloads this
/// assembly on every rebuild, so a surface that leaves a signal connected strands the whole
/// object graph behind it.</item>
/// </list>
/// </remarks>
internal interface ICreateDialogDocSurface
{
    /// <summary>
    /// Connects to <paramref name="parts"/>. Returns <c>false</c> — having connected nothing — when
    /// the dialog lacks a widget this surface requires.
    /// </summary>
    bool TryAttach(CreateDialogParts parts);

    /// <summary>Disconnects everything <see cref="TryAttach"/> connected. Idempotent.</summary>
    void Detach();
}
#endif
