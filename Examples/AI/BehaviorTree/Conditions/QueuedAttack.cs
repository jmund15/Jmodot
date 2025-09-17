namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.Movement;
using Implementation.AI.BB;

[GlobalClass]
[Tool]
public partial class QueuedAttack : BTCondition
{
    #region TASK_VARIABLES

    [Export] private bool _resetQueueIfTrue;
    private ICharacterController3D _moveComp;

    public QueuedAttack()
    {
        this._resetQueueIfTrue = false;
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        this._moveComp = this.BB.GetVar<ICharacterController3D>(BBDataSig.MoveComp);
        this.ConditionName = "_QueuedAttack";
    }

    public override void Enter()
    {
        base.Enter();
        if (!this.BB.GetPrimVar<bool>(BBDataSig.QueuedNextAttack).Value /*&&
            !_moveComp.WantsAttack()*/) // if no attack is queued
        {
            GD.Print("ATTACK WAS NOT QUEUED, EXITING TASK WITH FAILURE!");
            this.OnExitTask();
        }
        else
        {
            GD.Print("Attack was queued, continuing task!");
            if (this._resetQueueIfTrue) this.BB.SetPrimVar(BBDataSig.QueuedNextAttack, false);
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
