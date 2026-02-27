namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BehaviorTree.Conditions;
using Implementation.Shared;

/// <summary>
/// A BTCondition that aborts after a randomized duration in [MinDuration, MaxDuration].
/// Includes the _isActive guard to handle the Check()-before-OnParentTaskEnter() ordering.
/// Each OnParentTaskEnter() picks a fresh random duration.
///
/// With SucceedOnAbort=true: parent task aborts with Success when time expires.
/// Reusable on any BehaviorTask — wander timing, idle pauses, charge windows, etc.
/// </summary>
[GlobalClass, Tool]
public partial class RandomTimeLimit : BTCondition
{
    [Export(PropertyHint.Range, "0.0, 60.0, 0.1, or_greater")]
    private float _minDuration = 1.0f;

    [Export(PropertyHint.Range, "0.0, 60.0, 0.1, or_greater")]
    private float _maxDuration = 5.0f;

    private double _startTime;
    private float _currentLimit;
    private bool _isActive;

    public override void OnParentTaskEnter()
    {
        _startTime = Time.GetTicksMsec();
        _currentLimit = GetRandomDuration(_minDuration, _maxDuration);
        _isActive = true;
    }

    public override void OnParentTaskExit()
    {
        _isActive = false;
    }

    public override bool Check()
    {
        if (!_isActive) { return true; }
        return (Time.GetTicksMsec() - _startTime) / 1000.0 < _currentLimit;
    }

    /// <summary>
    /// Returns a random duration in [min, max]. Exposed as static for testability.
    /// </summary>
    public static float GetRandomDuration(float min, float max)
    {
        if (min >= max) { return min; }
        return JmoRng.GetRndInRange(min, max);
    }

    #region Test Helpers
#if TOOLS
    internal void SetMinDuration(float value) => _minDuration = value;
    internal void SetMaxDuration(float value) => _maxDuration = value;
#endif
    #endregion
}
