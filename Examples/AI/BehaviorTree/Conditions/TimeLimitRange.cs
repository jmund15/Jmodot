namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.Shared;

[GlobalClass]
[Tool]
public partial class TimeLimitRange : BTCondition
{
    #region TASK_VARIABLES

    private float _min;
    private float _max;

    [Export(PropertyHint.Range, "0.0, 10.0, 0.1, or_greater")]
    public float LimitMin
    {
        get => this._min;
        set
        {
            if (value == this._min)
            {
                return;
            }

            this._min = value;
            this.LimitMax = Mathf.Max(this._max, this._min);
        }
    }

    [Export(PropertyHint.Range, "0.0, 10.0, 0.1, or_greater")]
    public float LimitMax
    {
        get => this._max;
        set
        {
            if (value == this._max)
            {
                return;
            }

            this._max = value;
            this.LimitMin = Mathf.Clamp(this._min, 0.0f, this._max);
        }
    }

    private float _limit;
    private float _elasped;

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        this.ConditionName = $"_TimeLimitRange:{this.LimitMin}-{this.LimitMax}";
    }

    public override void Enter()
    {
        base.Enter();
        this._elasped = 0f;
        this._limit = MiscUtils.GetRndInRange(this.LimitMin, this.LimitMax);
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
        this._elasped += delta;
        //GD.Print("TIMELIMITRANGE CURRENT ELAPSED = ", _elasped);
        if (this._elasped >= this._limit)
        {
            //GD.Print("TIME LIMIT RANGE CONDITION FULL ELASPED");
            this.OnExitTask();
        }
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
    }

    #endregion
}
