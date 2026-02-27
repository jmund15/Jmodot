namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using Core.AI;
using Core.AI.Navigation.Considerations;
using Jmodot.AI.Navigation;
using Shared;
using GColl = Godot.Collections;

/// <summary>
/// A BehaviorAction that registers steering considerations on enter and unregisters
/// them on exit. Returns Running indefinitely — duration control is delegated to
/// BTConditions (TimeLimit, RandomTimeLimit, etc.).
///
/// This enables composable steering behaviors: attach wander considerations for wander,
/// flee considerations for flee — same action class, different data.
/// </summary>
[GlobalClass, Tool]
public partial class SteeringBehaviorAction : BehaviorAction
{
    [Export] private GColl.Array<BaseAIConsideration3D> _considerations = new();

    private AISteeringProcessor3D? _cachedSteering;

    protected override void OnEnter()
    {
        base.OnEnter();

        if (!TryGetSteering(out var steering))
        {
            JmoLogger.Error(this, "No AISteeringProcessor3D found on agent — cannot register considerations.");
            Status = TaskStatus.Failure;
            return;
        }

        foreach (var consideration in _considerations)
        {
            steering.RegisterConsideration(consideration);
        }
    }

    protected override void OnExit()
    {
        base.OnExit();

        if (!TryGetSteering(out var steering)) { return; }

        foreach (var consideration in _considerations)
        {
            steering.UnregisterConsideration(consideration);
        }
    }

    private bool TryGetSteering(out AISteeringProcessor3D steering)
    {
        if (_cachedSteering != null)
        {
            steering = _cachedSteering;
            return true;
        }

        if (Agent.TryGetFirstChildOfType(out AISteeringProcessor3D? found))
        {
            _cachedSteering = found;
            steering = found;
            return true;
        }

        steering = null!;
        return false;
    }

    #region Test Helpers
#if TOOLS
    internal void AddConsideration(BaseAIConsideration3D consideration) => _considerations.Add(consideration);
#endif
    #endregion
}
