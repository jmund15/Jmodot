namespace Jmodot.Implementation.Persistence.Snapshots;

using Godot;

/// <summary>
/// The persisted payload of one capture point: a schema version, the capture point's id and an ordered list of
/// participant slices. An ordered typed array is used instead of a dictionary because an editor resave strips
/// inline <c>Dictionary&lt;K, Resource&gt;</c> values.
/// </summary>
[GlobalClass, Tool]
public partial class SnapshotData : Resource
{
    /// <summary>Drives forward migration in the owning repository; the game bumps it when any slice changes shape.</summary>
    [Export] public int SchemaVersion { get; set; } = 1;

    /// <summary>The id of the capture point that produced this data.</summary>
    [Export] public StringName PointId { get; set; } = new();

    /// <summary>Slices in registration order. An entry may be null when its script failed to load.</summary>
    [Export] public Godot.Collections.Array<SnapshotSlice> Slices { get; set; } = new();

    /// <summary>
    /// Returns the first non-null slice whose <see cref="SnapshotSlice.SliceKey"/> equals <paramref name="key"/>, or
    /// null when absent. A key that appears on more than one slice is reported by
    /// <see cref="SnapshotCoordinator.Restore"/> as mismatched and not restored.
    /// </summary>
    public SnapshotSlice? FindSlice(StringName key)
    {
        foreach (var slice in this.Slices)
        {
            if (slice != null && slice.SliceKey == key)
            {
                return slice;
            }
        }

        return null;
    }
}
