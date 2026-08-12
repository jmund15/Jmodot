# C# Doc Tooltips

Surfaces each `[Export]`'s `/// <summary>` as its Godot Inspector tooltip, and each `[GlobalClass]`
class's `/// <summary>` as its description in the Create-New-Node dialog.

Hover an export's **value widget** and its summary appears. Nothing is configured — write the doc
comment you would write anyway, rebuild, and the tooltip is there.

## Installing it in a project

Godot's plugin convention puts the entry script under `res://addons/<name>/`, which this submodule
cannot own, so the project supplies a small `EditorPlugin` and this supplies the mechanism.

1. Create `res://addons/csharp_doc_tooltips/plugin.cfg` with `script="DocTooltipPlugin.cs"`.
2. Beside it, add the entry point:

```csharp
#if TOOLS
namespace YourGame.Addons.CsharpDocTooltips;

using Godot;
using Jmodot.Tools.DocTooltips.Editor;

[Tool]
public partial class DocTooltipPlugin : EditorPlugin
{
    private DocTooltipInstallation? _installation;

    public override void _EnterTree() => this._installation = DocTooltipInstallation.Install(this);

    public override void _ExitTree()
    {
        this._installation?.Uninstall();
        this._installation = null;
    }

    public override string _GetPluginName() => "C# Doc Tooltips";
}
#endif
```

3. Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in the project's `.csproj`.
4. Enable the plugin in `project.godot` under `[editor_plugins] enabled`.

`Install` reads the doc sidecar of **the host plugin's own assembly**. Declaring the entry point in
your project is what makes it find your game's summaries rather than Jmodot's — the mechanism never
resolves off a type in this folder, which would silently read Jmodot's sidecar wherever Jmodot compiles
as its own assembly.

## Why this exists

Godot does not do this for C#. At `4.7.1-stable`, `modules/mono/csharp_script.h` reads:

```cpp
virtual Vector<DocData::ClassDoc> get_documentation() const override {
    // TODO
    Vector<DocData::ClassDoc> docs;
    return docs;
}
```

The engine is handed no C# doc data at all, so the text has to come from the XML sidecar the
compiler emits (`<GenerateDocumentationFile>`). Upstream PRs #120450, #118210 and #83505 are open and
milestoned `4.x`.

## How it works

- `DocTooltipInstallation` owns the index and registers `DocTooltipInspectorPlugin` on the host in
  `Install`, removing it in `Uninstall`. Nothing is static: the editor unloads the .NET assembly on
  every rebuild, and a static would strand a multi-megabyte parsed dictionary and root the
  `AssemblyLoadContext`.
- Summaries resolve by **reflection over the live object**, walking `Type.BaseType` — not by the
  attached script's filename. In the source project, about a third of visible export slots are declared
  on an ancestor and ~170 concrete classes have exports inherited outright; filename keying drops all
  of them.
- The pure helpers live in `DocLookup/` (`DocSummaryResolver`, `XmlDocIndex`, `DocMemberKey`,
  `DocTooltipText` for header + wrapping, `ClassSummaryLookup` for class-name to summary). They carry no
  Godot types and are unit-testable from a consuming project's Logic suites.

### The one non-obvious constraint

Tooltip text is set on the row's **Control descendants**, never on the `EditorProperty` row itself.
`EditorInspector::update_tree()` stamps each row's `tooltip_text` with a doc-ID key
(`property|<Class>|<prop>`) and sets a private `has_doc_tooltip` flag; on hover, a flagged row is
parsed by `EditorHelpBit::parse_symbol`, which `ERR_FAIL`s on anything with fewer than three
pipe-separated slices. Prose on the row is an error path. A descendant carries neither the flag nor a
`make_custom_tooltip` override, and Godot resolves tooltips by walking up from the control under the
cursor — so prose renders normally there.

## Tooltip formatting

Each tooltip is a header line, a blank line, then the summary. The header reads
`Property <Name>: <type>` for exports and `Class <Name>` for classes.

The type is resolved by reflection walking the `Type.BaseType` chain most-derived-first, property
spelling before field, matching the doc-ID resolution order — so the header names the type of the
very member whose summary was found. Private fields are included because Godot exports them, and
inherited private members are invisible to a flattened reflection lookup at any `BindingFlags`.

Summaries are hard-wrapped at `DocTooltipText.WrapColumn` (90) characters. The wrap must be baked
into the string because Godot's default tooltip is a `PopupPanel` + `Label`, the `Label` has
autowrap off, and per Godot's own `Control` docs the tooltip "is shrunk to minimal size". Nothing
downstream inserts line breaks.

Wrapping is by character count, approximate under the proportional editor font. A word longer than
the column overflows onto its own line rather than being split, because summaries name identifiers
and `res://` paths.

Rich formatting (bold, colored type) is NOT possible: it needs `_MakeCustomTooltip`, a virtual on the
control that SUPPLIES the tooltip text, and that control is a built-in Inspector widget the addon
does not own and cannot attach a script to.

## Create-New-Node class descriptions

The Create-New-Node dialog's `Description:` panel says "No description available." for every C#
class; this fills it from the class's `/// <summary>`.

### Two surfaces, one setting

Project setting `addons/csharp_doc_tooltips/create_dialog_descriptions`, an enum: `Off`,
`Match List Tooltips`, `Description Panel`. Default: `Description Panel`. Editable in Project Settings;
changing it re-applies live (`CreateDialogDocs` watches `ProjectSettings.settings_changed`). The key is
namespaced under the addon's canonical folder, the way every published Godot addon owns its
`res://addons/<name>/` path.

- `Description Panel` (surface B, `DescriptionPanelSurface`) fills the dialog's own Description
  panel. Looks correct; depends on editor internals.
- `Match List Tooltips` (surface A, `MatchListTooltipSurface`) sets hover tooltips on rows of the
  Matches list. Public API only; cannot break the same way.

Both surfaces implement `ICreateDialogDocSurface`; the setting selects between them. The setting
exists because the two differ in risk, not just appearance.

### Why there is no supported hook

`CreateDialog` and `EditorHelpBit` are editor-internal, absent from Godot's script-exposed classes.
There is no `EditorCreateDialogPlugin` equivalent to the `EditorInspectorPlugin` the Inspector half
uses.

At `4.7.1-stable`, `create_dialog.cpp:681` calls `help_bit->parse_symbol("class|" + p_type + "|")`,
resolving against `EditorHelp::get_doc_data()` — the same database C# never populates.
"No description available." is generated at `editor_help.cpp:4793`.

The list rows are equally dead: `create_dialog.cpp:485-486` sets each row's tooltip from
`class_doc->value.brief_description`, empty for C#.

The engine's own escape hatch is barred: it writes arbitrary prose into that panel via
`EditorHelpBit::set_custom_text()` at `create_dialog.cpp:338` (that is how "No results for X"
appears), but `EditorHelpBit::_bind_methods()` registers ONE signal (`request_hide`) and ZERO
methods. So no `Object.Call("set_custom_text", ...)` can reach it. Public in C++ and callable from
C# are unrelated properties in Godot — everything crossing into scripting must be registered in
`_bind_methods()`.

### How surface B works, and how it breaks

What it writes is the `content` `RichTextLabel` inside the `EditorHelpBit`. `RichTextLabel` IS a
public exposed type; only its owner is internal.

The label is located structurally, not by index: content is the child with the vertical Expand size
flag (`content->set_v_size_flags(SIZE_EXPAND_FILL)` in `EditorHelpBit`'s constructor; `title` never
sets v flags). Index-based lookup would silently redirect the write to the title if a sibling were
inserted.

Ordering: selecting a row runs `CreateDialog::_item_selected` → `parse_symbol()` → `_update_labels()`
synchronously from the Tree's `cell_selected` signal. The addon connects to that SAME signal, so its
handler lands later in the same invocation list and overwrites rather than races.

The text is not permanent. `_update_labels()` also runs on
`NOTIFICATION_THEME_CHANGED` and rebuilds the label from the engine's `help_data`, discarding the
text. Changing the editor theme blanks the description back to "No description available." until the
next row selection repaints it.

A failed lookup leaves the panel ALONE and never clears it — every C++ class in that list has a real
engine description, so clearing on a miss would destroy correct text.

Text goes in via `RichTextLabel.AddText`, never the `Text` property, because the label parses BBCode
and a summary naming `[Export]` or `[Tool]` would be eaten as markup. No manual wrapping here —
unlike the plain `Label` behind a tooltip, this label autowraps.

If the content label cannot be located, the addon logs one `[DocTooltips]`
warning and automatically attaches the Match List Tooltips surface instead, so summaries stay
reachable. Fix the locator in `CreateDialogParts`, or set the project setting to
`Match List Tooltips` to make the fallback the authored choice.

### Finding the dialogs

Dialogs are found by walking the editor control tree once, deferred, at plugin enable — matching on
`GetClass() == "CreateDialog"`, which works even for unbound types.

Safe because the editor builds its docks before enabling plugins, and an assembly rebuild re-enters
the plugin.

Known hole: a CreateDialog constructed lazily by a dock opened later is missed. It degrades to the
engine's own empty description, never to an error.

The dialog holds TWO `Tree`s — Favorites and Matches — and Favorites is constructed first, so "first
Tree in the walk" finds the WRONG one. Matches is located from the search box instead: the nearest
ancestor of the search box owning a Tree as a direct child.

The class a row stands for is read exactly as `CreateDialog::get_selected_type_name()` does it — the
row text up to the first space — because a keyword match appends `"      - Matches the ... keyword."`
to the text.

Only `[GlobalClass]` types are resolvable, since those are the ones the dialog can name.

## Known coverage holes

- **Hovering the property *label* shows nothing.** That path is the engine's own doc lookup, which is
  empty for C#. Value widget shows the summary; the label one pixel away does not. This is the
  contract, not a bug.
- **`<inheritdoc/>` is not expanded** by the compiler into the sidecar, so members documented only
  that way have no tooltip.
- **`<see cref="..."/>` renders as its raw target text**, not a link — a tooltip has no renderer for
  markup.
- **Orphaned doc comments (CS1587) have no tooltip.** A `///` block placed between an attribute and
  its member — `[ExportGroup]` included — is dropped by the compiler and never reaches the sidecar.
  Put the `///` above **all** of a member's attributes.
- **Changing the editor theme blanks a Create-dialog description** until the next row selection
  (surface B only; see that section).
- **A class must be `[GlobalClass]`** to get a description in the Create dialog — the dialog cannot
  name any other C# type.

## Failure reporting

If the sidecar is missing, `Install` logs one warning naming the expected path and no-ops. If the
sidecar predates the assembly beside it by more than `DocSidecarLocator.SameBuildTolerance`, it logs
one staleness warning — tooltips will show the previous build's text until you rebuild. The tolerance
is not slack: MSBuild writes the doc file during compilation and the assembly at the end, so a healthy
same-build pair always has the sidecar a few seconds older. A sidecar that is present, fresh, and parses
to zero members gets its own warning, because every other signal looks healthy in that case.

## Where the sidecar is found

`DocSidecarLocator.Candidates` tries the assembly's own folder first
(`Path.ChangeExtension(Assembly.Location, ".xml")`, zero authored path segments), then each directory
under `res://.godot/mono/temp/bin`, freshest first. The second rung is not redundant — **Godot loads
the game assembly from a byte stream** so the DLL stays unlocked for hot-reload, and a stream-loaded
assembly reports `Assembly.Location` as the empty string. In the editor, which is the only place this
addon runs, the first rung yields nothing.
