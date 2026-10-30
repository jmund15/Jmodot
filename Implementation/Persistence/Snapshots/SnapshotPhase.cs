namespace Jmodot.Implementation.Persistence.Snapshots;

/// <summary>
/// When a participant restores relative to the game's world build. The framework does not define "world": a game
/// calls <see cref="SnapshotCoordinator.Restore"/> for each phase at the moments it chooses.
/// </summary>
public enum SnapshotPhase
{
    /// <summary>Restored by the game's first <see cref="SnapshotCoordinator.Restore"/> call, before it builds its world.</summary>
    PreWorld,

    /// <summary>Restored by the game's second <see cref="SnapshotCoordinator.Restore"/> call, after it builds its world.</summary>
    PostWorld,
}
