namespace Jmodot.Implementation.Persistence.Snapshots;

using System.Collections.Generic;
using Godot;

/// <summary>
/// The outcome of one <see cref="SnapshotCoordinator.Restore"/> phase. Restore never defaults a participant's state:
/// a participant listed in <see cref="Missing"/>, <see cref="Mismatched"/> or <see cref="Failed"/> was left as it was,
/// and the game decides whether a non-clean report aborts the resume.
/// </summary>
public sealed class SnapshotRestoreReport
{
    private readonly List<StringName> _restored = new();
    private readonly List<StringName> _missing = new();
    private readonly List<StringName> _mismatched = new();
    private readonly List<StringName> _failed = new();

    /// <summary>Participants whose restore delegate applied their slice.</summary>
    public IReadOnlyList<StringName> Restored => this._restored;

    /// <summary>Participants of the phase with no slice in the data (a null entry counts as no slice).</summary>
    public IReadOnlyList<StringName> Missing => this._missing;

    /// <summary>Participants whose slice is the wrong type, or whose key appears on more than one slice.</summary>
    public IReadOnlyList<StringName> Mismatched => this._mismatched;

    /// <summary>Participants whose restore delegate returned false or threw.</summary>
    public IReadOnlyList<StringName> Failed => this._failed;

    /// <summary>True when <see cref="Missing"/>, <see cref="Mismatched"/> and <see cref="Failed"/> are all empty.</summary>
    public bool IsClean => this._missing.Count == 0 && this._mismatched.Count == 0 && this._failed.Count == 0;

    internal void AddRestored(StringName key) => this._restored.Add(key);

    internal void AddMissing(StringName key) => this._missing.Add(key);

    internal void AddMismatched(StringName key) => this._mismatched.Add(key);

    internal void AddFailed(StringName key) => this._failed.Add(key);
}
