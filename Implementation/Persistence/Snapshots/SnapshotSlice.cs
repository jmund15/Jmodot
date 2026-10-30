namespace Jmodot.Implementation.Persistence.Snapshots;

using Godot;

/// <summary>
/// One participant's captured state inside a <see cref="SnapshotData"/>. A game subclasses this with plain
/// <c>[Export]</c> data fields; behavior lives in the delegates registered on <see cref="SnapshotCoordinator"/>.
/// </summary>
/// <remarks>
/// Every concrete subclass must be <c>[GlobalClass, Tool]</c> so the saver writes its script identity and the
/// editor can load a file that references it.
/// </remarks>
[GlobalClass, Tool]
public abstract partial class SnapshotSlice : Resource
{
    /// <summary>
    /// The registration key this slice was captured under. Set by <see cref="SnapshotCoordinator.Capture"/>;
    /// a participant never sets it.
    /// </summary>
    [Export] public StringName SliceKey { get; set; } = new();
}
