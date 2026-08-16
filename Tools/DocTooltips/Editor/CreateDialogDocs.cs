#if TOOLS
namespace Jmodot.Tools.DocTooltips.Editor;

using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Shared;
using Jmodot.Tools.DocTooltips.DocLookup;
using GCol = Godot.Collections;

/// <summary>
/// Puts C# class summaries into the Create-New-Node dialog, through whichever surface the project
/// setting selects.
/// </summary>
/// <remarks>
/// The Inspector half of this addon hangs off <c>EditorInspectorPlugin</c>, a supported extension
/// point. There is no counterpart for this dialog: <c>CreateDialog</c> is unbound, no plugin hook
/// fires when it opens, and the description it shows comes from <c>EditorHelp::get_doc_data()</c>,
/// which C# never populates. So the dialogs are found by walking the editor's control tree once,
/// after the editor has built its docks — plugins are enabled after that, and a rebuild re-enters
/// this plugin, so a single pass covers both cases. Dialogs constructed lazily by a dock opened
/// later are missed; they degrade to the engine's own empty description, never to an error.
///
/// The surface is authored rather than inferred because the two differ in risk, not just in looks —
/// see <see cref="ICreateDialogDocSurface"/>. Switching the setting re-applies live.
///
/// A <see cref="GodotObject"/> so the engine addresses its settings-watch and its deferred attach by
/// ObjectID; see <see cref="DocTooltipInstallation"/> for the folder invariant that requires it. The
/// owner frees this instance, so it must not be referenced after <see cref="Detach"/>.
/// </remarks>
[Tool]
internal sealed partial class CreateDialogDocs : GodotObject
{
    /// <summary>Which surface carries the class description, or none.</summary>
    internal enum Surface
    {
        /// <summary>Leave the dialog untouched.</summary>
        Off = 0,

        /// <summary>Row hover tooltips in the Matches list — public API only.</summary>
        MatchListTooltips = 1,

        /// <summary>The dialog's own Description panel — writes into an editor-internal widget.</summary>
        DescriptionPanel = 2,
    }

    /// <summary>Project setting selecting the surface. Authored in Project Settings, applied live.</summary>
    // Namespaced under the addon's canonical folder, the way every published Godot addon owns its
    // res://addons/<name>/ path — this is a convention reference, not a dependency on one consumer.
    internal const string SettingPath = "addons/csharp_doc_tooltips/create_dialog_descriptions";

    private const Surface DefaultSurface = Surface.DescriptionPanel;

    private readonly ClassSummaryLookup _lookup;
    private readonly List<ICreateDialogDocSurface> _attached = new();
    private bool _watchingSettings;

    internal CreateDialogDocs(ClassSummaryLookup lookup)
    {
        this._lookup = lookup;
    }

    /// <summary>
    /// Declares the setting so it appears in Project Settings with a named dropdown instead of a
    /// bare integer. Writes <c>project.godot</c> only on the run that first introduces it.
    /// </summary>
    internal static void RegisterSetting()
    {
        bool isNew = !ProjectSettings.HasSetting(SettingPath);
        if (isNew)
        {
            ProjectSettings.SetSetting(SettingPath, (int)DefaultSurface);
        }

        ProjectSettings.SetInitialValue(SettingPath, (int)DefaultSurface);
        ProjectSettings.AddPropertyInfo(new GCol.Dictionary
        {
            { "name", SettingPath },
            { "type", (int)Variant.Type.Int },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", "Off,Match List Tooltips,Description Panel" },
        });

        if (isNew)
        {
            ProjectSettings.Save();
        }
    }

    /// <summary>
    /// Watches the setting and attaches the authored surface. Public and instance-bound because the
    /// engine addresses it by ObjectID as a deferred call from <see cref="DocTooltipInstallation"/>.
    /// </summary>
    public void Attach()
    {
        if (!this._watchingSettings)
        {
            ProjectSettings.Singleton.Connect(
                ProjectSettings.SignalName.SettingsChanged, this.SettingsWatch());
            this._watchingSettings = true;
        }

        this.AttachSurfaces();
    }

    internal void Detach()
    {
        if (this._watchingSettings)
        {
            ProjectSettings.Singleton.Disconnect(
                ProjectSettings.SignalName.SettingsChanged, this.SettingsWatch());
            this._watchingSettings = false;
        }

        this.DetachSurfaces();
    }

    // ProjectSettings outlives every assembly load, so this connection is the one most able to
    // outlive US — it is ObjectID-bound per the folder invariant, never a delegate.
    private Callable SettingsWatch() => new(this, MethodName.OnSettingsChanged);

    /// <summary>
    /// Re-applies the authored surface. Public so the settings connection can address it by
    /// ObjectID; never call it directly.
    /// </summary>
    /// <remarks>
    /// ProjectSettings fires for ANY setting, so the whole re-apply has to be cheap and idempotent
    /// rather than conditional on which key moved — the signal carries no key.
    /// </remarks>
    public void OnSettingsChanged()
    {
        this.DetachSurfaces();
        this.AttachSurfaces();
    }

    private void DetachSurfaces()
    {
        foreach (ICreateDialogDocSurface surface in this._attached)
        {
            surface.Detach();
            surface.Free();
        }

        this._attached.Clear();
    }

    private void AttachSurfaces()
    {
        var mode = (Surface)ProjectSettings.GetSetting(SettingPath, (int)DefaultSurface).AsInt32();
        if (mode == Surface.Off) { return; }

        Control? editorRoot = EditorInterface.Singleton?.GetBaseControl();
        if (editorRoot == null || !GodotObject.IsInstanceValid(editorRoot)) { return; }

        var dialogs = new List<Node>();
        CreateDialogParts.CollectDialogs(editorRoot, dialogs);

        if (dialogs.Count == 0)
        {
            JmoLogger.Warning(
                "[DocTooltips]",
                "No CreateDialog found in the editor tree — Create-New-Node class descriptions are " +
                $"inactive. Set '{SettingPath}' to Off to silence this.");
            return;
        }

        foreach (Node dialog in dialogs)
        {
            this.AttachTo(dialog, mode);
        }
    }

    private void AttachTo(Node dialog, Surface mode)
    {
        if (!CreateDialogParts.TryLocate(dialog, out CreateDialogParts parts))
        {
            JmoLogger.Warning(
                "[DocTooltips]",
                "A CreateDialog's widgets could not be located — its structure has changed since " +
                "4.7.1. Create-New-Node class descriptions are inactive for it.");
            return;
        }

        ICreateDialogDocSurface surface = mode == Surface.DescriptionPanel
            ? new DescriptionPanelSurface(this._lookup)
            : new MatchListTooltipSurface(this._lookup);

        if (surface.TryAttach(parts))
        {
            this._attached.Add(surface);
            return;
        }

        // Only the Description panel can fail this way, and only by its content label having moved.
        // Degrading to the public-API surface keeps the summaries reachable — the warning is what
        // turns a silent nothing into a decision: fix the locator, or author the fallback.
        JmoLogger.Warning(
            "[DocTooltips]",
            "The Create-New-Node description panel could not be located (EditorHelpBit layout " +
            $"changed?). Falling back to match-list tooltips; set '{SettingPath}' to Match List " +
            "Tooltips to make that the authored choice.");

        var fallback = new MatchListTooltipSurface(this._lookup);
        if (fallback.TryAttach(parts))
        {
            this._attached.Add(fallback);
        }
    }
}
#endif
