namespace Jmodot.Implementation.Movement.Quirks;

using System;
using Core.AI.BB;
using Core.Movement.Quirks;
using Core.Shared;
using Shared;

/// <summary>
/// Snaps the agent sideways off its intended heading at randomized intervals, alternating sides.
/// The recovery half is free: the movement strategy hauls velocity back toward the desired
/// direction, so a jerk self-cancels without a tween or a state machine.
/// </summary>
[GlobalClass, Tool]
public partial class LateralJerkQuirk3D : MovementQuirk3D
{
    /// <summary>Shortest gap between jerks, in seconds.</summary>
    [Export(PropertyHint.Range, "0.05, 5.0, 0.05")] private float _minInterval = 0.25f;

    /// <summary>Longest gap between jerks, in seconds.</summary>
    [Export(PropertyHint.Range, "0.05, 5.0, 0.05")] private float _maxInterval = 1.0f;

    /// <summary>Sideways impulse magnitude, in units per second.</summary>
    [Export] private float _impulseMagnitude = 4.0f;

    /// <summary>Horizontal speed the agent must exceed before jerks fire — keeps them out of windups.</summary>
    [Export] private float _minSpeed = 0.5f;

    private bool _warnedNoSeed;

    public override MovementQuirkRuntime CreateRuntime(IBlackboard? blackboard, IRng? rngOverride = null)
    {
        var rng = rngOverride
                  ?? EntityRngResolver.Resolve(blackboard, SeedKinds.MovementQuirk, this, ref _warnedNoSeed);

        return new LateralJerkRuntime(rng)
        {
            TimeUntilNextJerk = rng.GetRndInRange(MinIntervalBound, MaxIntervalBound),
            Side = rng.GetRndSign(),
        };
    }

    public override void Tick(MovementQuirkRuntime runtime, in MovementQuirkContext3D ctx, float delta)
    {
        if (runtime is not LateralJerkRuntime jerk) { return; }

        var flatDirection = new Vector3(ctx.DesiredDirection.X, 0f, ctx.DesiredDirection.Z);
        if (flatDirection.IsZeroApprox()) { return; }

        var flatVelocity = new Vector3(ctx.AgentVelocity.X, 0f, ctx.AgentVelocity.Z);
        if (flatVelocity.Length() < _minSpeed) { return; }

        // The countdown runs only on gated-in frames, so an idle agent cannot bank intervals and
        // discharge them the moment it starts moving.
        jerk.TimeUntilNextJerk -= delta;
        if (jerk.TimeUntilNextJerk > 0f) { return; }

        jerk.TimeUntilNextJerk = jerk.Rng.GetRndInRange(MinIntervalBound, MaxIntervalBound);

        var lateral = flatDirection.Normalized().Cross(Vector3.Up) * jerk.Side;
        jerk.Side = -jerk.Side;

        ctx.Movement.ApplyImpulse(lateral * _impulseMagnitude);
    }

    private float MinIntervalBound => Math.Min(_minInterval, _maxInterval);

    private float MaxIntervalBound => Math.Max(_minInterval, _maxInterval);

    #region Test Helpers
#if TOOLS
    internal void SetTuning(float minInterval, float maxInterval, float impulseMagnitude, float minSpeed)
    {
        _minInterval = minInterval;
        _maxInterval = maxInterval;
        _impulseMagnitude = impulseMagnitude;
        _minSpeed = minSpeed;
    }
#endif
    #endregion
}

internal sealed class LateralJerkRuntime : MovementQuirkRuntime
{
    public float TimeUntilNextJerk;

    /// <summary>+1 or -1 — which side the next jerk goes to. Flips on every fire.</summary>
    public float Side;

    public LateralJerkRuntime(IRng rng) : base(rng) { }
}
