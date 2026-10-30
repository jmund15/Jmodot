namespace Jmodot.Implementation.Persistence.Snapshots;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Composes game state into a <see cref="SnapshotData"/> at a capture point and restores it on resume. Systems
/// register a capture delegate and a restore delegate; no gameplay class implements an interface.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Register{TSlice}"/> throws <see cref="ArgumentException"/> on an empty or duplicate key.
/// <see cref="Capture"/> calls every capture delegate in registration order and lets their exceptions propagate; a
/// delegate returning null throws <see cref="InvalidOperationException"/>. A capture delegate must return a detached
/// slice sharing no collection or Resource reference with live state, because <see cref="Latest"/> outlives later
/// mutation.
/// </para>
/// <para>
/// <see cref="Restore"/> throws <see cref="ArgumentNullException"/> on null data. A restore delegate must copy out of
/// its slice and never adopt the slice's collections. It returns false when it did not apply the slice; one that
/// returns false or throws is recorded in <see cref="SnapshotRestoreReport.Failed"/> and the phase continues.
/// </para>
/// </remarks>
public sealed class SnapshotCoordinator
{
    private readonly List<Participant> _participants = new();

    /// <summary>The most recent <see cref="Capture"/> result, or null before the first capture and after <see cref="ClearLatest"/>.</summary>
    public SnapshotData? Latest { get; private set; }

    /// <summary>
    /// Adds a participant: <paramref name="capture"/> produces its slice at each <see cref="Capture"/>, and
    /// <paramref name="restore"/> applies a slice of type <typeparamref name="TSlice"/> during
    /// <paramref name="phase"/>, returning false when it did not apply it. <paramref name="key"/> names the slice in
    /// the saved data and must be unique and non-empty. Registration order is capture order and restore order within a
    /// phase. Throws <see cref="ArgumentNullException"/> for a null delegate and <see cref="ArgumentException"/> for an
    /// empty or duplicate key.
    /// </summary>
    public void Register<TSlice>(
        StringName key,
        Func<TSlice> capture,
        Func<TSlice, bool> restore,
        SnapshotPhase phase = SnapshotPhase.PreWorld) where TSlice : SnapshotSlice
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(restore);
        if (key == null || key.IsEmpty)
        {
            throw new ArgumentException("A snapshot participant key must not be empty.", nameof(key));
        }

        if (this.IndexOf(key) >= 0)
        {
            throw new ArgumentException($"A snapshot participant is already registered under '{key}'.", nameof(key));
        }

        this._participants.Add(new Participant(
            key,
            phase,
            () => capture(),
            slice => slice is TSlice,
            slice => restore((TSlice)slice)));
    }

    /// <summary>Removes the participant registered under <paramref name="key"/>; false when none is registered.</summary>
    public bool Unregister(StringName key)
    {
        var index = this.IndexOf(key);
        if (index < 0)
        {
            return false;
        }

        this._participants.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Calls every capture delegate in registration order, sets each slice's <see cref="SnapshotSlice.SliceKey"/>,
    /// stores the result in <see cref="Latest"/> and returns it. A second call replaces <see cref="Latest"/>.
    /// </summary>
    public SnapshotData Capture(StringName pointId)
    {
        var data = new SnapshotData { PointId = pointId };
        foreach (var participant in this._participants)
        {
            var slice = participant.Capture();
            if (slice == null)
            {
                throw new InvalidOperationException($"The capture delegate for '{participant.Key}' returned null.");
            }

            slice.SliceKey = participant.Key;
            data.Slices.Add(slice);
        }

        this.Latest = data;
        return data;
    }

    /// <summary>Drops <see cref="Latest"/>, for example after a successful flush to disk.</summary>
    public void ClearLatest() => this.Latest = null;

    /// <summary>
    /// Restores the participants registered for <paramref name="phase"/>, in registration order, from
    /// <paramref name="data"/>. Never defaults a participant's state.
    /// </summary>
    public SnapshotRestoreReport Restore(SnapshotData data, SnapshotPhase phase)
    {
        ArgumentNullException.ThrowIfNull(data);

        var report = new SnapshotRestoreReport();
        foreach (var participant in this._participants)
        {
            if (participant.Phase != phase)
            {
                continue;
            }

            var matches = CountSlices(data, participant.Key);
            if (matches == 0)
            {
                report.AddMissing(participant.Key);
                continue;
            }

            var slice = data.FindSlice(participant.Key);
            if (matches > 1 || slice == null || !participant.Accepts(slice))
            {
                report.AddMismatched(participant.Key);
                continue;
            }

            if (this.TryApply(participant, slice))
            {
                report.AddRestored(participant.Key);
                continue;
            }

            report.AddFailed(participant.Key);
        }

        return report;
    }

    /// <summary>Keys of the slices in <paramref name="data"/> that no registered participant claims, in data order.</summary>
    public IReadOnlyList<StringName> FindUnclaimed(SnapshotData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var unclaimed = new List<StringName>();
        foreach (var slice in data.Slices)
        {
            if (slice != null && this.IndexOf(slice.SliceKey) < 0 && !unclaimed.Contains(slice.SliceKey))
            {
                unclaimed.Add(slice.SliceKey);
            }
        }

        return unclaimed;
    }

    private static int CountSlices(SnapshotData data, StringName key)
    {
        var count = 0;
        foreach (var slice in data.Slices)
        {
            if (slice != null && slice.SliceKey == key)
            {
                count++;
            }
        }

        return count;
    }

    private bool TryApply(Participant participant, SnapshotSlice slice)
    {
        try
        {
            if (participant.Apply(slice))
            {
                return true;
            }

            JmoLogger.Warning(this, $"[Snapshot] restore delegate for '{participant.Key}' did not apply its slice.");
            return false;
        }
        catch (Exception ex)
        {
            JmoLogger.Warning(this, $"[Snapshot] restore delegate for '{participant.Key}' threw: {ex.Message}");
            return false;
        }
    }

    private int IndexOf(StringName key)
    {
        for (var i = 0; i < this._participants.Count; i++)
        {
            if (this._participants[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }

    private sealed record Participant(
        StringName Key,
        SnapshotPhase Phase,
        Func<SnapshotSlice?> Capture,
        Func<SnapshotSlice, bool> Accepts,
        Func<SnapshotSlice, bool> Apply);
}
