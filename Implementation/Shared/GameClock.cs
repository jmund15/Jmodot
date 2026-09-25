namespace Jmodot.Implementation.Shared;

using System;
using Godot;

/// <summary>
/// The game-time source. It accumulates the physics delta, so game time is scaled by
/// <see cref="Engine.TimeScale"/> and stops while the tree is paused; the wall clock
/// (<see cref="Time.GetTicksMsec"/>) stays for telemetry and profiling only.
/// </summary>
/// <remarks>
/// The consuming project hosts exactly one instance in its always-present tree and reads it through
/// <see cref="Current"/>; the instance publishes itself on enter and clears on exit. On entering the tree the
/// clock makes itself pausable, whatever its host's process mode, and takes the lowest physics priority, so it
/// advances before every other physics reader and all readers in one tick see the same time. A read with no
/// instance throws rather than falling back to the wall clock.
/// </remarks>
public partial class GameClock : Node
{
    /// <summary>The live clock, or null before the hosting tree has entered.</summary>
    public static GameClock? Current { get; private set; }

    /// <summary>Game time in seconds accumulated by this clock since it was created.</summary>
    public double Seconds { get; private set; }

    /// <summary>Game time in seconds on the live clock. Throws <see cref="InvalidOperationException"/> when no clock is hosted.</summary>
    public static double NowSeconds => Required().Seconds;

    /// <summary>Game time in whole milliseconds on the live clock. Throws <see cref="InvalidOperationException"/> when no clock is hosted.</summary>
    public static ulong NowMsec => (ulong)(Required().Seconds * 1000.0);

    /// <summary>
    /// Creates a gameplay timer on <paramref name="owner"/>'s tree that pauses with the tree, follows
    /// <see cref="Engine.TimeScale"/> and ticks on the physics step, so its duration is game time.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="owner"/> is not inside a scene tree.</exception>
    public static SceneTreeTimer CreateTimer(Node owner, double seconds)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!owner.IsInsideTree())
        {
            throw new InvalidOperationException($"{nameof(GameClock)}.{nameof(CreateTimer)}: owner '{owner.Name}' is not inside a scene tree.");
        }
        return owner.GetTree().CreateTimer(seconds, processAlways: false, processInPhysics: true);
    }

    private static GameClock Required()
        => Current ?? throw new InvalidOperationException(
            $"No {nameof(GameClock)} is current: register a {nameof(GameClock)} node in the scene tree before gameplay reads time.");

    /// <summary>Publishes this clock as <see cref="Current"/> if no live clock is already registered.</summary>
    public override void _EnterTree()
    {
        if (Current != null && Current != this && IsInstanceValid(Current))
        {
            JmoLogger.Warning(this, $"[GameClock] A second GameClock entered the tree ('{Name}'); keeping the first clock Current.");
            ProcessMode = ProcessModeEnum.Disabled;
            QueueFree();
            return;
        }
        Current = this;
        ProcessMode = ProcessModeEnum.Pausable;
        // Lowest priority: a reader that runs earlier in the tick would see the previous tick's time.
        ProcessPhysicsPriority = int.MinValue;
    }

    /// <summary>Clears <see cref="Current"/> only when this clock is the current one.</summary>
    public override void _ExitTree()
    {
        if (Current == this) { Current = null; }
    }

    /// <summary>Advances <see cref="Seconds"/> by the scaled physics delta; a paused tree skips it.</summary>
    public override void _PhysicsProcess(double delta) => Seconds += delta;

    #region Test Helpers
#if TOOLS
    internal static void SetCurrentForTesting(GameClock? clock) => Current = clock;

    internal void SetSecondsForTesting(double seconds) => Seconds = seconds;

    internal void AdvanceForTesting(double seconds) => Seconds += seconds;
#endif
    #endregion
}
