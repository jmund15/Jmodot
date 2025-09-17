namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.Shared;

[GlobalClass]
[Tool]
public partial class ProbabilityToRun : BTCondition
{
    #region TASK_VARIABLES

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float RunProbability { get; set; }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
    }

    public override void Enter()
    {
        base.Enter();
        if (MiscUtils.Rnd.NextSingle() > this.RunProbability)
        {
            this.OnExitTask();
        }
    }

    public override void Exit()
    {
        base.Exit();
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
