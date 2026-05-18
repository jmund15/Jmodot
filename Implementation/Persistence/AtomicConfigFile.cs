namespace Jmodot.Implementation.Persistence;

using System;
using System.Threading;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Atomic write / corrupt-tolerant read utilities for Godot <see cref="ConfigFile"/>
/// instances persisted under <c>user://</c>.
/// </summary>
/// <remarks>
/// Shares the (write tmp, delete destination, rename tmp → destination) recipe with
/// <see cref="AtomicResourceFile"/>; the same Windows-atomicity caveat applies. A
/// corrupted file is renamed to <c>&lt;path&gt;.corrupt.&lt;unix_ts&gt;</c> on
/// <see cref="LoadOrCreate"/> so it can be inspected later, and a fresh empty
/// <see cref="ConfigFile"/> is returned.
/// </remarks>
public static class AtomicConfigFile
{
    private const string LogTag = "AtomicConfigFile";
    private const int RenameRetryCount = 3;
    private const int RenameRetryDelayMs = 50;

    public static ConfigFile LoadOrCreate(string userPath)
    {
        var cfg = new ConfigFile();

        if (!FileAccess.FileExists(userPath))
        {
            return cfg;
        }

        var loadErr = cfg.Load(userPath);
        if (loadErr == Error.Ok)
        {
            return cfg;
        }

        // Corrupt or unreadable. Backup-then-empty: rename to a timestamped sidecar
        // so a forensic look is possible, then return a fresh empty config.
        var backupPath = $"{userPath}.corrupt.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var renameErr = RenameWithRetry(userPath, backupPath);
        if (renameErr != Error.Ok)
        {
            JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] LoadOrCreate: load failed ({loadErr}) AND backup rename failed ({renameErr}) for '{userPath}'; corrupt file left in place");
        }
        else
        {
            JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] LoadOrCreate: corrupt config at '{userPath}' (load returned {loadErr}); backed up to '{backupPath}'");
        }

        return new ConfigFile();
    }

    public static Error SaveAtomic(ConfigFile cfg, string userPath)
    {
        var tmpPath = MakeTempPath(userPath);

        var saveErr = cfg.Save(tmpPath);
        if (saveErr != Error.Ok)
        {
            CleanupTemp(tmpPath);
            JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] SaveAtomic: ConfigFile.Save failed for tmp '{tmpPath}': {saveErr}");
            return saveErr;
        }

        if (FileAccess.FileExists(userPath))
        {
            var delErr = DirAccess.RemoveAbsolute(userPath);
            if (delErr != Error.Ok)
            {
                CleanupTemp(tmpPath);
                JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] SaveAtomic: failed to delete destination '{userPath}': {delErr} (tmp cleaned)");
                return delErr;
            }
        }

        var renameErr = RenameWithRetry(tmpPath, userPath);
        if (renameErr != Error.Ok)
        {
            // Preserve tmp: destination already deleted. No LoadOrCreate recovery path exists today
            // (follow-up in worklog), so the tmp is the only manual-recovery breadcrumb.
            JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] SaveAtomic: rename '{tmpPath}' -> '{userPath}' failed after {RenameRetryCount} retries: {renameErr}; tmp preserved at '{tmpPath}'");
            return renameErr;
        }

        return Error.Ok;
    }

    private static string MakeTempPath(string userPath)
    {
        var dot = userPath.LastIndexOf('.');
        return dot < 0 ? userPath + ".tmp" : userPath.Substring(0, dot) + ".tmp" + userPath.Substring(dot);
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

    private static void CleanupTemp(string tmpPath)
    {
        if (FileAccess.FileExists(tmpPath))
        {
            DirAccess.RemoveAbsolute(tmpPath);
        }
    }
}
