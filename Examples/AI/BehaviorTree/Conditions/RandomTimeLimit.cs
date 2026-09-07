namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using System;
using Core.AI.BB;
using Core.AI.BehaviorTree.Conditions;
using Core.Shared;
using Implementation.AI.BehaviorTree.Tasks;
using Implementation.Shared;

/// <summary>
/// A BTCondition that aborts after a randomized duration in [MinDuration, MaxDuration].
/// Includes the _isActive guard to handle the Check()-before-OnParentTaskEnter() ordering.
/// Each OnParentTaskEnter() picks a fresh random duration.
///
/// With SucceedOnAbort=true: parent task aborts with Success when time expires.
/// Reusable on any BehaviorTask — wander timing, idle pauses, charge windows, etc.
/// <para>
/// Draws from a per-owner-task seeded stream resolved from <see cref="BBDataSig.EntitySeed"/>
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
    public override void Init(BehaviorTask owner, Node agent, IBlackboard bb)
    {
        base.Init(owner, agent, bb);
        _rng = ResolveRng();
    }

    public override void OnParentTaskEnter()
    {
        _startTime = Time.GetTicksMsec();
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
        return (Time.GetTicksMsec() - _startTime) / 1000.0 < _currentLimit;
    }

    /// <summary>
    /// Returns a seeded random duration in [min, max], band-normalized (an inverted min/max is
    /// swapped rather than trusted, so an authoring mistake degrades gracefully instead of
    /// throwing). The sole owner of this roll-in-band primitive — <c>DitherAction</c>'s flip
    /// clock draws its own interval from the same function rather than forking a copy.
    /// </summary>
    public static float GetRandomDuration(IRng rng, float min, float max) => RollInRange(min, max, rng.GetRndFloat());

    /// <summary>
    /// Pure-math draw from an inclusive <paramref name="min"/>..<paramref name="max"/> band: a
    /// <paramref name="roll"/> of 0 returns <paramref name="min"/> and 1 returns
    /// <paramref name="max"/>, with the roll clamped to [0,1] and an inverted band normalized.
    /// RNG ownership lives at the call site so this function is pure-CLR testable without Godot
    /// runtime.
    /// </summary>
    public static float RollInRange(float min, float max, float roll)
    {
        float lo = Math.Min(min, max);
        float hi = Math.Max(min, max);
        return lo + Math.Clamp(roll, 0f, 1f) * (hi - lo);
    }

    #region Test Helpers
#if TOOLS
    internal void SetMinDuration(float value) => _minDuration = value;
    internal void SetMaxDuration(float value) => _maxDuration = value;
    internal float CurrentLimitForTest => _currentLimit;
#endif
    #endregion
}
