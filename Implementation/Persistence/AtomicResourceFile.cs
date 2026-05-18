namespace Jmodot.Implementation.Persistence;

using System.Threading;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Atomic write / read / delete utilities for Godot <see cref="Resource"/> instances
/// persisted under <c>user://</c>.
/// </summary>
/// <remarks>
/// <para>
/// "Atomic" here means: <c>WriteAtomic</c> never leaves the target file in a partially-written state.
/// On POSIX this is true atomicity via <c>rename(2)</c>. On Windows the recipe degrades to
/// (write tmp, delete destination, rename tmp → destination) because
/// <c>DirAccess.RenameAbsolute</c> wraps <c>MoveFileEx</c> without <c>MOVEFILE_REPLACE_EXISTING</c>
/// — a crash between the delete and rename leaves the previous file deleted and the new content
/// at <c>&lt;path&gt;.tmp</c>. <see cref="ReadIfExists{T}"/> recovers that interrupted state
/// on the next load.
/// </para>
/// <para>
/// A retry loop (3× / 50ms) around the rename rides out transient
/// <c>ERROR_SHARING_VIOLATION</c> from Windows antivirus scan-on-close handles.
/// </para>
/// </remarks>
public static class AtomicResourceFile
{
    private const string LogTag = "AtomicResourceFile";
    private const int RenameRetryCount = 3;
    private const int RenameRetryDelayMs = 50;

    public static Error WriteAtomic<T>(T resource, string userPath) where T : Resource
    {
        var tmpPath = MakeTempPath(userPath);

        var saveErr = ResourceSaver.Save(resource, tmpPath);
        if (saveErr != Error.Ok)
        {
            CleanupTemp(tmpPath);
            JmoLogger.Warning(LogTag, $"[Persistence:AtomicIO] WriteAtomic: ResourceSaver.Save failed for '{tmpPath}': {saveErr}");
            return saveErr;
        }

        if (FileAccess.FileExists(userPath))
        {
            var delErr = DirAccess.RemoveAbsolute(userPath);
            if (delErr != Error.Ok)
            {
                CleanupTemp(tmpPath);
                JmoLogger.Warning(LogTag, $"[Persistence:AtomicIO] WriteAtomic: failed to delete destination '{userPath}': {delErr} (tmp cleaned)");
                return delErr;
            }
        }

        var renameErr = RenameWithRetry(tmpPath, userPath);
        if (renameErr != Error.Ok)
        {
            // Preserve tmp: destination is already deleted, so the tmp is the only durable
            // copy of the new content. ReadIfExists's recovery branch promotes it on next load.
            JmoLogger.Warning(LogTag, $"[Persistence:AtomicIO] WriteAtomic: rename '{tmpPath}' -> '{userPath}' failed after {RenameRetryCount} retries: {renameErr}; tmp preserved for ReadIfExists recovery");
            return renameErr;
        }

        return Error.Ok;
    }

    private static Error RenameWithRetry(string from, string to)
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
    /// Inserts <c>.tmp</c> before the final extension so <see cref="ResourceSaver"/>
    /// still keys off the original extension. <c>foo.tres</c> → <c>foo.tmp.tres</c>.
    /// </summary>
    private static string MakeTempPath(string userPath)
    {
        var dot = userPath.LastIndexOf('.');
        return dot < 0 ? userPath + ".tmp" : userPath.Substring(0, dot) + ".tmp" + userPath.Substring(dot);
    }

    private static void CleanupTemp(string tmpPath)
    {
        if (FileAccess.FileExists(tmpPath))
        {
            DirAccess.RemoveAbsolute(tmpPath);
        }
    }

    public static T? ReadIfExists<T>(
        string userPath,
        ResourceLoader.CacheMode cache = ResourceLoader.CacheMode.Reuse) where T : Resource
    {
        if (!FileAccess.FileExists(userPath))
        {
            var tmpPath = MakeTempPath(userPath);
            if (!FileAccess.FileExists(tmpPath))
            {
                return null;
            }

            JmoLogger.Warning(LogTag, $"[Persistence:AtomicIO] ReadIfExists: recovering interrupted write — completing rename '{tmpPath}' -> '{userPath}'");
            var renameErr = RenameWithRetry(tmpPath, userPath);
            if (renameErr != Error.Ok)
            {
                JmoLogger.Warning(LogTag, $"[Persistence:AtomicIO] ReadIfExists: recovery rename failed: {renameErr}; tmp left in place");
                return null;
            }
        }

        var loaded = ResourceLoader.Load(userPath, string.Empty, cache);
        if (loaded is T typed)
        {
            return typed;
        }

        JmoLogger.Warning(LogTag, $"[Persistence:AtomicIO] ReadIfExists: type mismatch at '{userPath}': expected {typeof(T).Name}, got {loaded?.GetType().Name ?? "<null>"}");
        loaded?.Dispose();
        return null;
    }

    public static Error DeleteIfExists(string userPath)
    {
        if (!FileAccess.FileExists(userPath))
        {
            return Error.Ok;
        }

        var err = DirAccess.RemoveAbsolute(userPath);
        if (err != Error.Ok)
        {
            JmoLogger.Warning(LogTag, $"[Persistence:AtomicIO] DeleteIfExists: failed to remove '{userPath}': {err}");
        }

        return err;
    }
}
