namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

/// <summary>
/// One entity's move/pause cycle state for a <see cref="MovementCadenceResource"/>: whether it is paused, the time left
/// in the current half, and a start phase so entities sharing one resource do not move in lockstep. Each consuming
/// action owns one clock.
/// </summary>
public struct MovementCadenceClock
{
    /// <summary>True while the cycle is in its pause half.</summary>
    public bool Paused { get; private set; }

    /// <summary>Seconds left in the current half.</summary>
    public float Timer { get; private set; }

    /// <summary>
    /// Places the clock <paramref name="unit"/> (in [0, 1)) of the way through the cycle, a cycle being the move
    /// half then the pause half. A cycle with no move window or no pause window starts moving.
    /// </summary>
    public void Start(float moveSeconds, float pauseSeconds, float unit)
    {
        Paused = false;
        Timer = moveSeconds > 0f ? moveSeconds : 0f;
        if (!(moveSeconds > 0f) || !(pauseSeconds > 0f)) { return; }

        float cycle = moveSeconds + pauseSeconds;
        float offset = unit * cycle;
        if (offset < moveSeconds)
        {
            Timer = moveSeconds - offset;
            return;
        }

        Paused = true;
        Timer = cycle - offset;
    }

    /// <summary>
    /// Advances the cycle by <paramref name="delta"/> and reports whether the entity may move this tick: false while
    /// paused. A cycle with no move window is continuous.
    /// </summary>
    public bool Advance(float moveSeconds, float pauseSeconds, float delta)
    {
        if (!(moveSeconds > 0f))
        {
            Paused = false;
            return true;
        }

        bool moving = MovementCadenceResource.AdvanceCadence(
            Paused, Timer, moveSeconds, pauseSeconds, delta, out bool paused, out float timer);
        Paused = paused;
        Timer = timer;
        return moving;
    }
}
