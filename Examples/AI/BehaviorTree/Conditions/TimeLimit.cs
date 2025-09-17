#region

using Jmodot.Core.AI.BB;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

[GlobalClass]
[Tool]
public partial class TimeLimit : BTCondition
{
    #region TASK_VARIABLES

    [Export] public float Limit { get; set; }

    private float _elasped;

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        _elasped = 0f;
    }

    public override void Enter()
    {
        base.Enter();
        _elasped = 0f;
    }

    public override void Exit()
    {
        base.Exit();
        _elasped = 0f;
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
        _elasped += delta;
        if (_elasped >= Limit) OnExitTask();
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
    }

    #endregion
}