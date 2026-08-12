namespace Jmodot.Tools.DocTooltips.DocLookup;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Facts about the XML documentation sidecar as a file: where it might be, and whether the one found
/// is behind the assembly it documents.
/// </summary>
/// <remarks>
/// Two rungs, because neither alone is sufficient. The sidecar sits beside the assembly that declares
/// the documented types, so the assembly's own path derives it with zero authored segments — but
/// Godot loads the game assembly from a byte stream to keep the DLL unlocked for hot-reload, and a
/// stream-loaded assembly reports <c>Assembly.Location</c> as the EMPTY string. That is the editor,
/// the only place the addon runs. The search directories are the fallback the caller supplies from
/// the engine's own build-output layout; they are engine-owned locations, never authored ones.
/// </remarks>
public static class DocSidecarLocator
{
    /// <summary>
    /// How much older than its assembly a sidecar may be and still count as same-build. MSBuild emits
    /// the doc file DURING compilation and writes the assembly at the end, so a healthy pair always
    /// has the sidecar older — measured 2s and 5s on this project. Without this window the freshness
    /// check reports every successful build as stale, which trains the reader to ignore it. Sized far
    /// above one build's internal write gap and far below the gap to any earlier build.
    /// </summary>
    public static readonly TimeSpan SameBuildTolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// True when the sidecar predates its assembly by more than <see cref="SameBuildTolerance"/> —
    /// i.e. the assembly was rebuilt and the docs were not.
    /// </summary>
    public static bool IsBehindAssembly(DateTime sidecarUtc, DateTime assemblyUtc)
    {
        return sidecarUtc < assemblyUtc - SameBuildTolerance;
    }

    /// <summary>
    /// Candidate sidecar paths in PRIORITY ORDER — the caller takes the first that exists. Order is
    /// the contract: the assembly's own sibling `.xml` comes first because it authors no path
    /// segments, then one path per search directory in the order given. These are candidates, not
    /// verified paths; none is guaranteed to exist and this never touches the filesystem.
    /// </summary>
    /// <param name="assemblyLocation">
    /// The documented assembly's file path. Empty or null is EXPECTED, not an error — Godot
    /// stream-loads the assembly, so this is the empty string in the editor; that rung is skipped.
    /// </param>
    /// <param name="assemblyName">Simple assembly name, used as the sidecar's file name.</param>
    /// <param name="searchDirectories">Fallback directories, most-preferred first.</param>
    public static IEnumerable<string> Candidates(
        string? assemblyLocation, string? assemblyName, IEnumerable<string>? searchDirectories)
    {
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            yield return Path.ChangeExtension(assemblyLocation, ".xml");
        }

        if (string.IsNullOrEmpty(assemblyName) || searchDirectories == null)
        {
            yield break;
        }

        foreach (string directory in searchDirectories)
        {
            if (string.IsNullOrEmpty(directory))
            {
                continue;
            }

            yield return Path.Combine(directory, assemblyName + ".xml");
        }
    }
}
