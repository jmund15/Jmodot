namespace Jmodot.Examples.AI.BehaviorTree.Tasks;

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

    protected override void OnEnter()
    {
        base.OnEnter();
        float effectiveDuration = CalculateEffectiveDuration(_duration, _additiveVariation);

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
    /// Calculates the effective wait duration.
    /// Returns duration + Random(0, additiveVariation) when variation > 0.
    /// Returns duration exactly when variation <= 0.
    /// Exposed as static for testability.
    /// </summary>
    public static float CalculateEffectiveDuration(float duration, float additiveVariation)
    {
        if (additiveVariation <= 0f)
        {
            return duration;
        }

        return duration + JmoRng.GetRndInRange(0f, additiveVariation);
    }

    #region Test Helpers
#if TOOLS
    internal void SetDuration(float value) => _duration = value;
    internal void SetAdditiveVariation(float value) => _additiveVariation = value;
#endif
    #endregion
}
