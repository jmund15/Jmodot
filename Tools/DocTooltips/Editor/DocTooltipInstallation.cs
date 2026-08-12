#if TOOLS
namespace Jmodot.Tools.DocTooltips.Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;
using Jmodot.Implementation.Shared;
using Jmodot.Tools.DocTooltips.DocLookup;

/// <summary>
/// Installs the Inspector decoration that turns each <c>[Export]</c>'s <c>/// &lt;summary&gt;</c> into that
/// row's tooltip, plus the Create-New-Node dialog's class descriptions, onto a host
/// <see cref="EditorPlugin"/>. The engine supplies no C# doc data of its own —
/// <c>CSharpScript::get_documentation()</c> is an empty stub at 4.7.1 — so the text comes from the XML
/// sidecar the compiler emits beside the assembly.
/// </summary>
/// <remarks>
/// Reads the sidecar of the HOST's own assembly, never its own: the host plugin type is defined in the
/// consuming project, so this resolves that project's game assembly under either Jmodot integration
/// model (source glob or assembly reference). Resolving off a type in this file would silently read
/// Jmodot's sidecar wherever Jmodot compiles separately.
///
/// Owns the index and resolver TRANSITIVELY: both are created in <see cref="Install"/> and reach this
/// instance only through <see cref="DocTooltipInspectorPlugin"/>, so nulling that one field in
/// <see cref="Uninstall"/> releases the whole chain. Deliberately NOT statics: the editor unloads and
/// reloads the .NET assembly on every rebuild, and a static would outlive that, stranding a
/// multi-megabyte parsed dictionary and rooting the AssemblyLoadContext. For the same reason teardown
/// is synchronous and nothing here subscribes to an editor-lifetime singleton — staleness is polled by
/// timestamp instead.
///
/// Contract: call <see cref="Install"/> from the host's <c>_EnterTree</c> and <see cref="Uninstall"/>
/// from its <c>_ExitTree</c>. Installing twice without an intervening uninstall strands the first
/// index; <see cref="Uninstall"/> is idempotent.
/// </remarks>
public sealed class DocTooltipInstallation
{
    private readonly EditorPlugin _host;
    private DocTooltipInspectorPlugin? _inspectorPlugin;
    private CreateDialogDocs? _createDialogDocs;

    private DocTooltipInstallation(EditorPlugin host)
    {
        this._host = host;
    }

    /// <summary>
    /// Parses <paramref name="host"/>'s XML doc sidecar and attaches both documentation surfaces.
    /// </summary>
    public static DocTooltipInstallation Install(EditorPlugin host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var installation = new DocTooltipInstallation(host);
        Assembly assembly = host.GetType().Assembly;

        string xmlPath = ResolveSidecarPath(assembly);
        var index = new XmlDocIndex(xmlPath);

        WarnIfUnusable(xmlPath, index);

        installation._inspectorPlugin = new DocTooltipInspectorPlugin(new DocSummaryResolver(index));
        host.AddInspectorPlugin(installation._inspectorPlugin);

        CreateDialogDocs.RegisterSetting();

        // One index behind both consumers: the parsed dictionary is multi-megabyte, and the two
        // resolvers ask it different questions rather than owning separate copies of the answer.
        installation._createDialogDocs = new CreateDialogDocs(
            new ClassSummaryLookup(new DocSummaryResolver(index), GlobalClasses(assembly)));

        // Deferred so the editor's docks — which own the dialogs — are guaranteed built and inside
        // the tree, whatever order plugin enabling lands in relative to them.
        CreateDialogDocs docs = installation._createDialogDocs;
        Callable.From(docs.Attach).CallDeferred();

        return installation;
    }

    /// <summary>Detaches both surfaces and releases the parsed index.</summary>
    public void Uninstall()
    {
        if (this._inspectorPlugin != null)
        {
            this._host.RemoveInspectorPlugin(this._inspectorPlugin);
        }

        this._createDialogDocs?.Detach();

        this._inspectorPlugin = null;
        this._createDialogDocs = null;
    }

    /// <summary>
    /// The assembly's <c>[GlobalClass]</c> types — the only ones the Create dialog can name.
    /// </summary>
    /// <remarks>
    /// A partially loadable assembly yields what it can rather than throwing: the exception carries
    /// the types that DID load, and losing tooltips for the rest beats aborting plugin registration.
    /// </remarks>
    private static IEnumerable<Type> GlobalClasses(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types;
        }

        foreach (Type? type in types)
        {
            if (type != null && type.IsDefined(typeof(GlobalClassAttribute), inherit: false))
            {
                yield return type;
            }
        }
    }

    /// <summary>
    /// First existing candidate from <see cref="DocSidecarLocator"/>, or empty when none is on disk.
    /// </summary>
    private static string ResolveSidecarPath(Assembly assembly)
    {
        foreach (string candidate in DocSidecarLocator.Candidates(
                     assembly.Location, assembly.GetName().Name, EngineBuildOutputDirectories()))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Godot's .NET build output, one directory per build configuration, freshest first. Enumerated
    /// rather than named so a configuration this project does not currently emit still resolves, and
    /// so an <c>Export*</c> folder left over from a previous export never outranks the editor's own
    /// current build.
    /// </summary>
    private static IEnumerable<string> EngineBuildOutputDirectories()
    {
        string root = ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin");
        if (!Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        try
        {
            // Materialised inside the try on purpose: a lazy sequence would defer the walk to the
            // caller's foreach, where a build recreating this directory mid-enumeration throws past
            // this guard and out of _EnterTree, aborting plugin registration.
            return Directory.EnumerateDirectories(root)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Reports the three ways the doc source can be wrong, once each at install time. Never per
    /// property and never per frame: this is an editor convenience, and a warning on the hover path
    /// would fire thousands of times per inspector rebuild.
    /// </summary>
    private static void WarnIfUnusable(string xmlPath, XmlDocIndex index)
    {
        if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
        {
            JmoLogger.Warning(
                "[DocTooltips]",
                $"No XML documentation sidecar at '{xmlPath}'. Inspector tooltips are disabled. " +
                "Add <GenerateDocumentationFile>true</GenerateDocumentationFile> to the consuming " +
                "project's .csproj and rebuild.");
            return;
        }

        // Present-but-stale is the common failure and the nastier one: the author edits a <summary>,
        // hovers before rebuilding, and reads the OLD text with no signal at all. A timestamp cache
        // notices the file CHANGED; it cannot notice the file is BEHIND its source. The sidecar and
        // the assembly are written by the same build into the same folder, so comparing them is
        // exactly the staleness question.
        // The DLL beside the sidecar, NOT Assembly.Location — Godot stream-loads the assembly, so
        // Location is empty here and File.Exists on it would silently skip this check entirely.
        string assemblyPath = System.IO.Path.ChangeExtension(xmlPath, ".dll");
        if (File.Exists(assemblyPath)
            && DocSidecarLocator.IsBehindAssembly(
                File.GetLastWriteTimeUtc(xmlPath), File.GetLastWriteTimeUtc(assemblyPath)))
        {
            JmoLogger.Warning(
                "[DocTooltips]",
                "XML documentation sidecar is older than the assembly beside it — Inspector tooltips " +
                "may show stale summaries. Rebuild to refresh.");
        }

        index.RefreshIfStale();

        // Present, fresh, and empty. XmlDocIndex swallows a malformed or unreadable sidecar by
        // design (a half-written file mid-build is transient, not a defect), so without this the
        // feature is silently dead in exactly the case where every other signal looks healthy.
        if (index.Count == 0)
        {
            JmoLogger.Warning(
                "[DocTooltips]",
                $"XML documentation sidecar at '{xmlPath}' parsed to zero members. Inspector tooltips " +
                "will be blank. The file is likely malformed, unreadable, or was captured mid-build — " +
                "rebuild, and check it contains <member name=\"P:...\"> entries.");
        }
    }
}
#endif
