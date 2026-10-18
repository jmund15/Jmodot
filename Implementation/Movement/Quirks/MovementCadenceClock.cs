namespace Jmodot.Implementation.Movement.Quirks;

/// <summary>
/// One entity's move/pause cycle state: whether it is paused, the time left in the current half,
/// and a start phase so entities sharing one cadence resource do not move in lockstep.
/// <para>
/// This type owns the cadence arithmetic. A runtime that drives a gait holds one clock; the legacy
/// steering actions hold one per action until they are retired.
/// </para>
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
        Timer = IsUsablePeriod(moveSeconds) ? moveSeconds : 0f;
        if (!IsUsablePeriod(moveSeconds) || !IsUsablePeriod(pauseSeconds)) { return; }

        // Double arithmetic: adding two finite negated floats can overflow to infinity, and the
        // wrapped offset must stay exact for phase continuity.
        double cycle = (double)moveSeconds + pauseSeconds;
        double offset = unit * cycle;
        if (offset < moveSeconds)
        {
            Timer = (float)(moveSeconds - offset);
            return;
        }

        Paused = true;
        Timer = (float)(cycle - offset);
    }

    /// <summary>
    /// Advances the cycle by <paramref name="delta"/> and reports whether voluntary drive is
    /// permitted this tick: false while paused. A non-positive or non-finite window always permits.
    /// </summary>
    public bool Advance(float moveSeconds, float pauseSeconds, float delta)
    {
        if (!IsUsablePeriod(moveSeconds) || !IsUsablePeriod(pauseSeconds))
        {
            Paused = false;
            Timer = IsUsablePeriod(moveSeconds) ? moveSeconds : 0f;
            return true;
        }

        bool moving = AdvanceCadence(
            Paused, Timer, moveSeconds, pauseSeconds, delta, out bool paused, out float timer);
        Paused = paused;
        Timer = timer;
        return moving;
    }

    /// <summary>
    /// Advances one move/pause cadence tick and reports whether voluntary drive may run. The
    /// parameter shape is the legacy arithmetic seam; <see cref="Advance"/> is the owning caller.
    /// </summary>
    internal static bool AdvanceCadence(bool wasPaused, float cadenceTimer, float moveSeconds,
        float pauseSeconds, float delta, out bool newPaused, out float newTimer)
    {
        if (!IsUsablePeriod(moveSeconds) || !IsUsablePeriod(pauseSeconds))
        {
            newPaused = false;
            newTimer = IsUsablePeriod(moveSeconds) ? moveSeconds : 0f;
            return true;
        }

        if (!(delta > 0f) || !float.IsFinite(delta))
        {
            newPaused = wasPaused;
            newTimer = cadenceTimer;
            return !wasPaused;
        }

        double move = moveSeconds;
        double pause = pauseSeconds;
        double cycle = move + pause;
        double timer = System.Math.Clamp((double)cadenceTimer, 0.0, wasPaused ? pause : move);
        // Position within the cycle, then carry the overshoot across every crossed boundary,
        // including whole cycles. Exact move-end enters pause; exact cycle-end enters move.
        double position = (wasPaused ? move + (pause - timer) : move - timer) + delta;
        double wrapped = position % cycle;
        if (wrapped < 0.0) { wrapped += cycle; }

        newPaused = wrapped >= move;
        newTimer = (float)(newPaused ? cycle - wrapped : move - wrapped);
        return !newPaused;
    }

    private static bool IsUsablePeriod(float seconds) => float.IsFinite(seconds) && seconds > 0f;
}
