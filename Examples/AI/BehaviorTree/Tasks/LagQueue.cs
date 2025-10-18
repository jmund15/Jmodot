namespace Jmodot.Examples.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using Core.AI;
using Core.AI.BB;
using Implementation.AI.BB;
using Implementation.AI.BehaviorTree.Tasks;

[GlobalClass]
[Tool]
public partial class LagQueue : BehaviorAction
{
    #region TASK_VARIABLES

    [Export(PropertyHint.Range, "0.0, 5.0, .1f, or_greater")]
    protected float LagTime;

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
    }

    protected override void OnEnter()
    {
        base.OnEnter();
        if (this.LagTime <= 0f)
        {
            this.OnLagTimeout();
            return;
        }

        this.GetTree().CreateTimer(this.LagTime).Timeout += this.OnLagTimeout;
    }

    protected override void OnExit()
    {
        base.OnExit();
    }

    protected override void OnProcessFrame(float delta)
    {
        base.OnProcessFrame(delta);
    }

    protected override void OnProcessPhysics(float delta)
    {
        base.OnProcessPhysics(delta);
    }

    #endregion

    #region TASK_HELPER

    protected virtual void OnLagTimeout()
    {
        if (this.BB.Get<bool>(BBDataSig.QueuedNextAttack))
        {
            this.Status = TaskStatus.SUCCESS;
        }
        else
        {
            this.Status = TaskStatus.FAILURE;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        //

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #endregion
}
