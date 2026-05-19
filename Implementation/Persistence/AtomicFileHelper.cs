namespace Jmodot.Implementation.Persistence;

using System.Threading;
using Godot;

/// <summary>
/// Shared low-level atomic-file primitives used by <see cref="AtomicResourceFile"/> and
/// <see cref="AtomicConfigFile"/>. Pure file-I/O — no logging side-effects; the calling
/// public methods own their own <c>LogTag</c> for caller-discriminable Warning emissions.
/// <para>
/// Memory rules pinned here: <c>arch_rule_atomic_rename_tmp_is_durable.md</c> (the rename
/// IS the commit; on failure tmp IS the new state) and
/// <c>gotcha_godot_resourcesaver_extension_dispatch.md</c> (insert <c>.tmp</c> BEFORE the
/// final extension, not after — <see cref="MakeTempPath"/>).
/// </para>
/// </summary>
public static class AtomicFileHelper
{
    public const int RenameRetryCount = 3;
    public const int RenameRetryDelayMs = 50;

    /// <summary>
    /// Compose a temp-path companion for <paramref name="userPath"/> by inserting
    /// <c>.tmp</c> immediately before the final extension. Paths without an extension
    /// receive a trailing <c>.tmp</c> instead.
    /// </summary>
    public static string MakeTempPath(string userPath)
    {
        var dot = userPath.LastIndexOf('.');
        return dot < 0 ? userPath + ".tmp" : userPath.Substring(0, dot) + ".tmp" + userPath.Substring(dot);
    }

    /// <summary>
    /// Rename <paramref name="from"/> to <paramref name="to"/> with bounded retries
    /// (<see cref="RenameRetryCount"/> attempts spaced by <see cref="RenameRetryDelayMs"/>).
    /// Returns the last observed <see cref="Error"/> on exhaustion. Caller is responsible
    /// for any logging or recovery on a non-OK return.
    /// </summary>
    public static Error RenameWithRetry(string from, string to)
    {
        var lastErr = Error.Ok;
        for (var attempt = 0; attempt < RenameRetryCount; attempt++)
        {
            lastErr = DirAccess.RenameAbsolute(from, to);
            if (lastErr == Error.Ok)
            {
                return Error.Ok;
            }

            if (attempt < RenameRetryCount - 1)
            {
                Thread.Sleep(RenameRetryDelayMs);
            }
        }

        return lastErr;
    }

    /// <summary>
    /// Remove the temp file at <paramref name="tmpPath"/> if it exists. Silently no-ops
    /// when the file is absent; does not log on failure.
    /// </summary>
    public static void CleanupTemp(string tmpPath)
    {
        if (FileAccess.FileExists(tmpPath))
        {
            DirAccess.RemoveAbsolute(tmpPath);
        }
    }
}
