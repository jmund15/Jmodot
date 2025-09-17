#region

using BTaskStatus = Jmodot.Implementation.AI.BehaviorTree.Tasks.BTaskStatus;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Implementation.AI.BB;

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

    public override void Enter()
    {
        base.Enter();
        if (this.LagTime <= 0f)
        {
            this.OnLagTimeout();
            return;
        }

        this.GetTree().CreateTimer(this.LagTime).Timeout += this.OnLagTimeout;
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
        /*if (BB.GetVar<ICharacterController3D>(BBDataSig.MoveComp).WantsAttack())
            BB.SetPrimVar(BBDataSig.QueuedNextAttack, true);*/
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
    }

    #endregion

    #region TASK_HELPER

    protected virtual void OnLagTimeout()
    {
        if (this.BB.GetPrimVar<bool>(BBDataSig.QueuedNextAttack).Value)
            this.Status = BTaskStatus.SUCCESS;
        else
            this.Status = BTaskStatus.FAILURE;
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        //

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #endregion
}
