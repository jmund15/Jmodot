#region

using Jmodot.Core.AI.BB;
using Jmodot.Core.Movement;
using Jmodot.Implementation.AI.BB;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

[GlobalClass]
[Tool]
public partial class QueuedAttack : BTCondition
{
    #region TASK_VARIABLES

    [Export] private bool _resetQueueIfTrue;
    private ICharacterController3D _moveComp;

    public QueuedAttack()
    {
        _resetQueueIfTrue = false;
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        _moveComp = BB.GetVar<ICharacterController3D>(BBDataSig.MoveComp);
        ConditionName = "_QueuedAttack";
    }

    public override void Enter()
    {
        base.Enter();
        if (!BB.GetPrimVar<bool>(BBDataSig.QueuedNextAttack).Value /*&&
            !_moveComp.WantsAttack()*/) // if no attack is queued
        {
            GD.Print("ATTACK WAS NOT QUEUED, EXITING TASK WITH FAILURE!");
            OnExitTask();
        }
        else
        {
            GD.Print("Attack was queued, continuing task!");
            if (_resetQueueIfTrue) BB.SetPrimVar(BBDataSig.QueuedNextAttack, false);
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