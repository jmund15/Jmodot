#region

using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BB;
using BTaskStatus = Jmodot.Implementation.AI.BehaviorTree.Tasks.BTaskStatus;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Tasks;

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
        if (LagTime <= 0f)
        {
            OnLagTimeout();
            return;
        }

        GetTree().CreateTimer(LagTime).Timeout += OnLagTimeout;
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
        if (BB.GetPrimVar<bool>(BBDataSig.QueuedNextAttack).Value)
            Status = BTaskStatus.SUCCESS;
        else
            Status = BTaskStatus.FAILURE;
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        //

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #endregion
}