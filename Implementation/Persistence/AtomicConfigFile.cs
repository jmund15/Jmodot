namespace Jmodot.Implementation.Persistence;

using System;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Atomic write / corrupt-tolerant read utilities for Godot <see cref="ConfigFile"/>
/// instances persisted under <c>user://</c>.
/// </summary>
/// <remarks>
/// Shares the (write tmp, delete destination, rename tmp → destination) recipe with
/// <see cref="AtomicResourceFile"/>; low-level path / rename / cleanup primitives live in
/// <see cref="AtomicFileHelper"/>. The same Windows-atomicity caveat applies. A corrupted
/// file is renamed to <c>&lt;path&gt;.corrupt.&lt;unix_ts&gt;</c> on <see cref="LoadOrCreate"/>
/// so it can be inspected later, and a fresh empty <see cref="ConfigFile"/> is returned.
/// </remarks>
public static class AtomicConfigFile
{
    private const string LogTag = "AtomicConfigFile";

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
        var renameErr = AtomicFileHelper.RenameWithRetry(userPath, backupPath);
        if (renameErr != Error.Ok)
        {
            JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] LoadOrCreate: load failed ({loadErr}) AND backup rename failed ({renameErr}) for '{userPath}'; corrupt file left in place");
        }
        else
        {
            JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] LoadOrCreate: corrupt config at '{userPath}' (load returned {loadErr}); backed up to '{backupPath}'");
        }

        // Discard cfg — it may carry partial state from the failed Load. Return a fresh
        // instance so the caller's defaults aren't merged with garbage.
        return new ConfigFile();
    }

    public static Error SaveAtomic(ConfigFile cfg, string userPath)
    {
        var tmpPath = AtomicFileHelper.MakeTempPath(userPath);

        var saveErr = cfg.Save(tmpPath);
        if (saveErr != Error.Ok)
        {
            AtomicFileHelper.CleanupTemp(tmpPath);
            JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] SaveAtomic: ConfigFile.Save failed for tmp '{tmpPath}': {saveErr}");
            return saveErr;
        }

        if (FileAccess.FileExists(userPath))
        {
            var delErr = DirAccess.RemoveAbsolute(userPath);
            if (delErr != Error.Ok)
            {
                AtomicFileHelper.CleanupTemp(tmpPath);
                JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] SaveAtomic: failed to delete destination '{userPath}': {delErr} (tmp cleaned)");
                return delErr;
            }
        }

        var renameErr = AtomicFileHelper.RenameWithRetry(tmpPath, userPath);
        if (renameErr != Error.Ok)
        {
            // Preserve tmp: destination already deleted. No LoadOrCreate recovery path exists today
            // (follow-up in worklog), so the tmp is the only manual-recovery breadcrumb.
            JmoLogger.Warning(LogTag, $"[Persistence:ConfigIO] SaveAtomic: rename '{tmpPath}' -> '{userPath}' failed after {AtomicFileHelper.RenameRetryCount} retries: {renameErr}; tmp preserved at '{tmpPath}'");
            return renameErr;
        }

        return Error.Ok;
    }
}
