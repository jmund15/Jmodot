namespace Jmodot.Implementation.Shared;

using System;
using System.Collections.Generic;
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

    // A remainder below this fraction of a tick is rounding: 1/60 s is inexact in binary, and a float [Export] widens to a
    // value just above its tick multiple, so without it a 0.2 s timer would count 13 ticks instead of 12.
    private const double TickRemainderTolerance = 1e-3;

    private static readonly List<PendingTimer> PendingTimers = new();

    private static GameClock? _timerDriver;

    /// <summary>
    /// Creates a gameplay timer on <paramref name="owner"/>'s tree that pauses with the tree, follows
    /// <see cref="Engine.TimeScale"/> and ticks on the physics step, so its duration is game time. It times out on the
    /// first physics tick after creation at which at least <paramref name="seconds"/> of game time has elapsed.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="owner"/> is not inside a scene tree, or no clock is ticking in the tree.</exception>
    public static SceneTreeTimer CreateTimer(Node owner, double seconds)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!owner.IsInsideTree())
        {
            throw new InvalidOperationException($"{nameof(GameClock)}.{nameof(CreateTimer)}: owner '{owner.Name}' is not inside a scene tree.");
        }
        if (_timerDriver == null)
        {
            throw new InvalidOperationException(
                $"{nameof(GameClock)}.{nameof(CreateTimer)}: no {nameof(GameClock)} is ticking in the scene tree to drive the timer.");
        }
        // Godot decrements a timer created earlier in this tick's physics pass during that same tick; one scaled tick of
        // headroom keeps it from firing before the clock takes over on the next tick.
        double headroom = Math.Max(1.0, Engine.TimeScale) / Engine.PhysicsTicksPerSecond;
        var timer = owner.GetTree().CreateTimer(seconds + headroom, processAlways: false, processInPhysics: true);
        PendingTimers.Add(new PendingTimer(timer, seconds));
        return timer;
    }

    // Runs before this tick's timer pass: an expired timer gets zero time left so the pass fires it, and a running one gets
    // exactly one tick more than its remainder so the pass leaves it at that remainder.
    private static void AdvanceTimers(double delta)
    {
        for (int i = 0; i < PendingTimers.Count; i++)
        {
            var pending = PendingTimers[i];
            pending.Remaining -= delta;
            if (pending.Remaining <= delta * TickRemainderTolerance)
            {
                pending.Timer.TimeLeft = 0;
                PendingTimers.RemoveAt(i--);
            }
            else
            {
                pending.Timer.TimeLeft = pending.Remaining + delta;
            }
        }
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
        _timerDriver = this;
        ProcessMode = ProcessModeEnum.Pausable;
        // Lowest priority: a reader that runs earlier in the tick would see the previous tick's time.
        ProcessPhysicsPriority = int.MinValue;
    }

    /// <summary>Clears <see cref="Current"/> only when this clock is the current one.</summary>
    public override void _ExitTree()
    {
        if (Current == this) { Current = null; }
        if (_timerDriver == this)
        {
            _timerDriver = null;
            PendingTimers.Clear();
        }
    }

    /// <summary>Advances <see cref="Seconds"/> and every pending timer by the scaled physics delta; a paused tree skips it.</summary>
    public override void _PhysicsProcess(double delta)
    {
        Seconds += delta;
        if (_timerDriver == this) { AdvanceTimers(delta); }
    }

    private sealed class PendingTimer
    {
        public PendingTimer(SceneTreeTimer timer, double remaining)
        {
            Timer = timer;
            Remaining = remaining;
        }

        public SceneTreeTimer Timer { get; }

        public double Remaining { get; set; }
    }

    #region Test Helpers
#if TOOLS
    internal static void SetCurrentForTesting(GameClock? clock) => Current = clock;

    internal void SetSecondsForTesting(double seconds) => Seconds = seconds;

    internal void AdvanceForTesting(double seconds) => Seconds += seconds;
#endif
    #endregion
}
