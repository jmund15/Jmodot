namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.AI.BehaviorTree.Conditions;
using Core.Shared;
using Implementation.AI.BehaviorTree.Tasks;
using Implementation.Shared;

/// <summary>
/// A condition that succeeds based on a random probability check.
/// This is a "guard" condition, meant to be checked once on entry.
/// Set MonitorConditions=false on the parent task for this behavior.
/// Rolls draw from a per-owner-task stream seeded from <see cref="Jmodot.Implementation.AI.BB.BBDataSig.EntitySeed"/>;
/// absent that slot the stream is <see cref="JmoRng.UnseededByDesign"/> and one warning is logged.
/// </summary>
[GlobalClass, Tool]
public partial class ProbabilityToRun : BTCondition
{
    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    public float RunProbability { get; private set; } = 0.5f;

    private bool _lastCheckResult;
    private bool _isActive;
    private IRng? _rng;
    private bool _warnedNoSeed;

    public override void Init(BehaviorTask owner, Node agent, IBlackboard bb)
    {
        base.Init(owner, agent, bb);
        _rng = ResolveRng();
    }

    public override void OnParentTaskEnter()
    {
        _lastCheckResult = (_rng ??= ResolveRng()).GetRndFloat() < RunProbability;
        _isActive = true;
    }

    private IRng ResolveRng()
    {
        var ownerName = OwnerTask != null && IsInstanceValid(OwnerTask) ? OwnerTask.Name.ToString() : "unbound";
        return EntityRngResolver.Resolve(BB, $"{SeedKinds.ProbabilityToRun}:{ownerName}", this, ref _warnedNoSeed);
    }

    public override void OnParentTaskExit()
    {
        _isActive = false;
    }

    public override bool Check()
    {
        if (!_isActive) { return true; }
        return _lastCheckResult;
    }

    #region Test Helpers
#if TOOLS
    internal void SetRunProbability(float value) => RunProbability = value;
#endif
    #endregion
}
