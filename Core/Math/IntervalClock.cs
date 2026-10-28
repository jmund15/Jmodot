namespace Jmodot.Core.Math;

using System;

/// <summary>
/// A fixed-interval cadence that keeps its leftover time. Each <see cref="Advance"/> adds elapsed
/// time and returns how many intervals came due, so an interval shorter than one step, or a step
/// that overshoots, still fires every due interval instead of dropping the remainder.
/// </summary>
/// <remarks>
/// A mutable struct: hold it in a non-readonly field and never copy it out, or the copy advances
/// instead of the field. A <c>default</c> clock runs at <see cref="MinimumInterval"/> with the
/// <see cref="DefaultMaxFires"/> cap.
/// </remarks>
public struct IntervalClock
{
    /// <summary>Default cap on fires returned by one <see cref="Advance"/>.</summary>
    public const int DefaultMaxFires = 8;

    /// <summary>The smallest interval; a zero, negative or non-finite interval clamps to it.</summary>
    public const float MinimumInterval = 0.001f;

    // Absorbs 1/60 accumulation and float-export rounding so a step landing on a boundary fires.
    private const double BoundaryTolerance = 1e-6;

    private float _interval;
    private readonly int _maxFires;

    /// <summary>Creates a clock with <paramref name="interval"/> seconds and a catch-up cap of <paramref name="maxFires"/>.</summary>
    public IntervalClock(float interval, int maxFires = DefaultMaxFires)
    {
        _interval = Clamp(interval);
        _maxFires = Math.Max(1, maxFires);
        Accumulated = 0;
    }

    /// <summary>The interval in seconds. Setting it keeps <see cref="Accumulated"/>.</summary>
    public float Interval
    {
        readonly get => _interval > 0 ? _interval : MinimumInterval;
        set => _interval = Clamp(value);
    }

    /// <summary>Seconds accumulated toward the next fire.</summary>
    public double Accumulated { readonly get; private set; }

    /// <summary>
    /// Adds <paramref name="delta"/> seconds and returns the number of intervals now due, keeping the
    /// remainder. A backlog past the cap returns the cap and drops the rest. A negative or non-finite
    /// delta fires nothing and changes nothing.
    /// </summary>
    public int Advance(double delta)
    {
        if (!(delta >= 0) || double.IsInfinity(delta)) { return 0; }

        Accumulated += delta;
        float interval = Interval;
        int maxFires = _maxFires > 0 ? _maxFires : DefaultMaxFires;
        double quotient = Math.Floor((Accumulated + BoundaryTolerance) / interval);
        if (quotient > maxFires)
        {
            Accumulated = 0;
            return maxFires;
        }
        int due = (int)quotient;
        Accumulated = Math.Max(0, Accumulated - due * (double)interval);
        return due;
    }

    /// <summary>
    /// Advances at <paramref name="interval"/>. A non-positive or non-finite <paramref name="interval"/>
    /// returns 1 (one fire per call) and leaves <see cref="Accumulated"/> unchanged; otherwise it sets
    /// <see cref="Interval"/> and returns <see cref="Advance(double)"/>.
    /// </summary>
    public int Advance(double delta, float interval)
    {
        if (!(interval > 0) || !float.IsFinite(interval)) { return 1; }

        Interval = interval;
        return Advance(delta);
    }

    /// <summary>Sets the accumulated time, e.g. to <see cref="Interval"/> so the next advance fires at once.</summary>
    public void Reset(double accumulated = 0) => Accumulated = Math.Max(0, accumulated);

    private static float Clamp(float interval)
        => float.IsFinite(interval) && interval > MinimumInterval ? interval : MinimumInterval;
}
