#region

using Jmodot.Core.AI.BB;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

[GlobalClass]
[Tool]
public partial class Cooldown : BTCondition
{
    #region TASK_HELPER

    private void OnCooldownTimeout()
    {
        _cooled = true;
    }

    #endregion

    #region TASK_VARIABLES

    [Export] private float _cooldownTime;

    private SceneTreeTimer _cooldownTimer;
    private bool _cooled;

    public Cooldown(float cooldownTime) // REAL????
    {
        _cooldownTime = cooldownTime;
    }

    public Cooldown()
    {
        _cooldownTime = 0f;
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
    }

    public override void Enter()
    {
        base.Enter();
        if (!_cooled) OnExitTask();
        //if (_cooldownTimer.TimeLeft > 0 && Status == ConditionStatus.SUCCESS)
        //{
        //	Status = ConditionStatus.FAILURE; // failsafe
        //}
    }

    public override void Exit()
    {
        base.Exit();
        if (!_cooled) return; // already cooling, don't restart cooldown

        _cooldownTimer = (Engine.GetMainLoop() as SceneTree).CreateTimer(_cooldownTime);
        _cooldownTimer.Timeout += OnCooldownTimeout;
        _cooled = false;
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
    }

    #endregion
}