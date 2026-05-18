namespace Jmodot.Examples.AI.BehaviorTree.Tasks;

using System;
using Core.AI;
using Implementation.AI.BehaviorTree.Tasks;
using Implementation.Shared;

/// <summary>
/// A BehaviorAction that waits for a duration and then succeeds.
/// Supports both fixed and variable durations via _additiveVariation.
/// When _additiveVariation is 0 (default), waits exactly _duration seconds.
/// When _additiveVariation > 0, waits _duration + Random(0, _additiveVariation) seconds.
/// </summary>
[GlobalClass, Tool]
public partial class Lag : BehaviorAction
{
    [Export(PropertyHint.Range, "0.1, 10.0, 0.1, or_greater")]
    private float _duration = 1.0f;

    [Export(PropertyHint.Range, "0.0, 10.0, 0.1, or_greater")]
    private float _additiveVariation = 0.0f;

    private SceneTreeTimer _timer = null!;
    private JmoRng? _rng;

    protected override void OnEnter()
    {
        base.OnEnter();
        _rng ??= JmoRng.NonDeterministic();
        float effectiveDuration = CalculateEffectiveDuration(_duration, _additiveVariation, _rng.GetRndFloat());

        if (effectiveDuration <= 0f)
        {
            Status = TaskStatus.Success;
            return;
        }

        _timer = GetTree().CreateTimer(effectiveDuration);
        _timer.Timeout += OnLagTimeout;
    }

    protected override void OnExit()
    {
        base.OnExit();
        if (_timer.IsValid())
        {
            _timer.Timeout -= OnLagTimeout;
        }
    }

    private void OnLagTimeout()
    {
        if (Status == TaskStatus.Running)
        {
            Status = TaskStatus.Success;
        }
    }

    /// <summary>
    /// Pure-math effective wait duration. Returns <paramref name="duration"/> exactly when
    /// <paramref name="additiveVariation"/> &lt;= 0; otherwise returns
    /// <c>duration + clamp01(variationRoll) * additiveVariation</c>.
    /// RNG ownership lives at the call site (see <see cref="OnEnter"/>) so this function
    /// is pure-CLR testable without Godot runtime.
    /// </summary>
    public static float CalculateEffectiveDuration(float duration, float additiveVariation, float variationRoll)
    {
        if (additiveVariation <= 0f)
        {
            return duration;
        }

        float clampedRoll = Math.Clamp(variationRoll, 0f, 1f);
        return duration + clampedRoll * additiveVariation;
    }

    #region Test Helpers
#if TOOLS
    internal void SetDuration(float value) => _duration = value;
    internal void SetAdditiveVariation(float value) => _additiveVariation = value;
#endif
    #endregion
}
