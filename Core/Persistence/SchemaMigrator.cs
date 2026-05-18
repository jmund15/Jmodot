namespace Jmodot.Core.Persistence;

using System;
using System.Collections.Generic;

/// <summary>
/// Forward-only schema migrator for persisted user-data Resources.
/// </summary>
/// <remarks>
/// Migrations are version-keyed: <c>migrations[v]</c> migrates a payload at version <c>v</c>
/// to version <c>v+1</c>. To migrate from <c>currentVersion</c> to <c>targetVersion</c>,
/// the migrator applies <c>migrations[currentVersion]</c>, <c>migrations[currentVersion+1]</c>,
/// ..., <c>migrations[targetVersion-1]</c> in order.
/// <para>
/// Forward-only by contract: a <c>targetVersion</c> less than <c>currentVersion</c> throws.
/// Save-slot rollback, if ever needed, uses a separate API.
/// </para>
/// <para>
/// Mid-chain failure semantics: <c>T data</c> is a reference; if a migration step throws,
/// the partial mutation is visible to the caller. Callers must not retain <c>data</c> if an
/// exception escapes — reload from disk or discard.
/// </para>
/// </remarks>
public static class SchemaMigrator
{
    public static int MigrateIfNeeded<T>(
        T data,
        int currentVersion,
        int targetVersion,
        IReadOnlyDictionary<int, Action<T>> migrations)
    {
        if (targetVersion < currentVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetVersion),
                $"Forward-only migration: targetVersion ({targetVersion}) must be >= currentVersion ({currentVersion}).");
        }

        for (var v = currentVersion; v < targetVersion; v++)
        {
            if (!migrations.TryGetValue(v, out var step))
            {
                throw new InvalidOperationException(
                    $"Migration chain incomplete: no migration registered for version {v} -> {v + 1} " +
                    $"(currentVersion={currentVersion}, targetVersion={targetVersion}).");
            }

            step(data);
        }

        return targetVersion;
    }
}
