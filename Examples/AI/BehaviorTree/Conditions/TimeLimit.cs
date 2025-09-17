namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;

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
        this._elasped = 0f;
    }

    public override void Enter()
    {
        base.Enter();
        this._elasped = 0f;
    }

    public override void Exit()
    {
        base.Exit();
        this._elasped = 0f;
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
        this._elasped += delta;
        if (this._elasped >= this.Limit)
        {
            this.OnExitTask();
        }
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
    }

    #endregion
}
