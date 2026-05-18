namespace Jmodot.Core.Persistence;

using System;

/// <summary>
/// Contract for a single-payload user-data save repository (settings, meta-progression, etc.).
/// </summary>
/// <remarks>
/// One repository owns one persisted artifact. Implementations live in the consuming project
/// (e.g. <c>PushinPotions/Global/Persistence/</c>) — Jmodot ships the contract and the
/// low-level atomic I/O utilities, not domain-specific repositories.
/// <para>
/// <see cref="Load"/> is expected to return a fresh in-memory representation; whether that
/// means parsing a stored file, returning a cached instance, or producing a default payload
/// when <see cref="HasSavedData"/> is <c>false</c> is up to the implementation.
/// </para>
/// <para>
/// <see cref="DataChanged"/> fires after a successful <see cref="Save"/> with the saved data,
/// to let UI / autoload consumers refresh without re-reading from disk.
/// </para>
/// </remarks>
/// <typeparam name="T">The persisted payload type. Reference type only.</typeparam>
public interface ISaveRepository<T> where T : class
{
    /// <summary>True when a persisted artifact exists on disk.</summary>
    bool HasSavedData { get; }

    /// <summary>Returns the persisted payload, or a fresh default when <see cref="HasSavedData"/> is false.</summary>
    T Load();

    /// <summary>Atomically persists <paramref name="data"/> and fires <see cref="DataChanged"/> on success.</summary>
    void Save(T data);

    /// <summary>Removes the persisted artifact. No-op when none exists.</summary>
    void Delete();

    /// <summary>
    /// Fires after a successful <see cref="Save"/> with the saved payload. Implementers MUST
    /// initialize with <c>= delegate { };</c> per project convention (csharp_patterns.md) to skip
    /// per-fire null checks.
    /// </summary>
    event Action<T>? DataChanged;
}
