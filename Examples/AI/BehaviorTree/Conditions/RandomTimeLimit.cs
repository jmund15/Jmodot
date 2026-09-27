namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.AI.BehaviorTree.Conditions;
using Core.Shared;
using Implementation.AI.BehaviorTree.Tasks;
using Implementation.Shared;
using Implementation.Shared.GodotExceptions;

/// <summary>
/// A BTCondition that aborts after a randomized duration in [MinDuration, MaxDuration].
/// Includes the _isActive guard to handle the Check()-before-OnParentTaskEnter() ordering.
/// Each OnParentTaskEnter() picks a fresh random duration.
///
/// With SucceedOnAbort=true: parent task aborts with Success when time expires.
/// Reusable on any BehaviorTask — wander timing, idle pauses, charge windows, etc.
/// <para>
/// Draws from a per-owner-task seeded stream resolved from <see cref="Jmodot.Implementation.AI.BB.BBDataSig.EntitySeed"/>
/// (same source <c>DitherAction</c> reads) at <see cref="Init"/>; falls back to
/// <see cref="JmoRng.UnseededByDesign"/> with one warning when the slot is absent.
/// </para>
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
    private IRng? _rng;
    private bool _warnedNoSeed;

    /// <inheritdoc />
    /// <exception cref="ResourceConfigurationException">A duration bound is NaN or infinite.</exception>
    public override void Init(BehaviorTask owner, Node agent, IBlackboard bb)
    {
        if (!float.IsFinite(_minDuration) || !float.IsFinite(_maxDuration))
        {
            throw new ResourceConfigurationException(
                $"RandomTimeLimit durations must be finite (min {_minDuration}, max {_maxDuration}).", this);
        }

        base.Init(owner, agent, bb);
        _rng = ResolveRng();
    }

    public override void OnParentTaskEnter()
    {
        _startTime = GameClock.NowSeconds;
        _currentLimit = GetRandomDuration(_rng ??= ResolveRng(), _minDuration, _maxDuration);
        _isActive = true;
    }

    /// <summary>
    /// Resolved lazily — on <see cref="Init"/>, or, absent one, on first
    /// <see cref="OnParentTaskEnter"/> — never as an eager field initializer, which would allocate
    /// a <see cref="RandomNumberGenerator"/> at this Resource's editor type-registration, before
    /// engine bootstrap completes.
    /// </summary>
    private IRng ResolveRng()
    {
        var ownerName = OwnerTask != null && IsInstanceValid(OwnerTask) ? OwnerTask.Name.ToString() : "unbound";
        return EntityRngResolver.Resolve(BB, $"{SeedKinds.RandomTimeLimit}:{ownerName}", this, ref _warnedNoSeed);
    }

    public override void OnParentTaskExit()
    {
        _isActive = false;
    }

    public override bool Check()
    {
        if (!_isActive) { return true; }
        return GameClock.NowSeconds - _startTime < _currentLimit;
    }

    /// <summary>
    /// Returns a seeded random duration in [min, max], drawn through <see cref="JmoMath.RollInRange"/>
    /// (an inverted min/max is normalized rather than trusted).
    /// </summary>
    public static float GetRandomDuration(IRng rng, float min, float max) => JmoMath.RollInRange(min, max, rng.GetRndFloat());

    #region Test Helpers
#if TOOLS
    internal void SetMinDuration(float value) => _minDuration = value;
    internal void SetMaxDuration(float value) => _maxDuration = value;
    internal float CurrentLimitForTest => _currentLimit;
#endif
    #endregion
}
