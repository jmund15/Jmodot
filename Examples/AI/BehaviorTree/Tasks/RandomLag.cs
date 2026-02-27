namespace Jmodot.Examples.AI.BehaviorTree.Tasks;

using Implementation.Shared;

/// <summary>
/// Extends Lag with a randomized duration. Each time the task enters,
/// LagTime is set to a random value in [MinDuration, MaxDuration].
/// </summary>
[GlobalClass, Tool]
public partial class RandomLag : Lag
{
    [Export(PropertyHint.Range, "0.0, 10.0, 0.1, or_greater")]
    private float _minDuration = 1.0f;

    [Export(PropertyHint.Range, "0.0, 10.0, 0.1, or_greater")]
    private float _maxDuration = 3.0f;

    protected override void OnEnter()
    {
        LagTime = GetRandomDuration(_minDuration, _maxDuration);
        base.OnEnter();
    }

    /// <summary>
    /// Returns a random duration in [min, max]. Exposed as static for testability.
    /// </summary>
    public static float GetRandomDuration(float min, float max)
    {
        if (min >= max)
        {
            return min;
        }

        return JmoRng.GetRndInRange(min, max);
    }

    #region Test Helpers
#if TOOLS
    internal void SetMinDuration(float value) => _minDuration = value;
    internal void SetMaxDuration(float value) => _maxDuration = value;
#endif
    #endregion
}
