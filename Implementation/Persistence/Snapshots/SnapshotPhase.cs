namespace Jmodot.Implementation.Persistence.Snapshots;

/// <summary>
/// When a participant restores relative to the game's world build. The framework does not define "world": a game
/// calls <see cref="SnapshotCoordinator.Restore"/> for each phase at the moments it chooses.
/// </summary>
public enum SnapshotPhase
{
    PreWorld,
    PostWorld,
}
