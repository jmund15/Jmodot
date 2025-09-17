namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.Movement;
using Implementation.AI.BB;

[GlobalClass]
[Tool]
public partial class QueueableAttack : BTCondition
{
    #region TASK_VARIABLES

    [Export] private float _queueBuffer;

    //[Export]
    //private bool _setQueueFalseOnEnter;
    private ICharacterController3D _moveComp;

    private bool _canQueue;
    private float _timeElapsed;

    public QueueableAttack()
    {
        this._queueBuffer = 0f;
        //_setQueueFalseOnEnter = false;
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        this._moveComp = this.BB.GetVar<ICharacterController3D>(BBDataSig.MoveComp);
        this.ConditionName = $"_QueueableAttack:{this._queueBuffer}";
    }

    public override void Enter()
    {
        base.Enter();
        //if (_setQueueFalseOnEnter)
        //{
        //          BB.SetPrimVar(BBDataSig.QueuedNextAttack, false);
        //      }
        this._timeElapsed = 0f;
        if (this._queueBuffer <= 0f)
        {
            this._canQueue = true;
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
        if (!this._canQueue)
        {
            this._timeElapsed += delta;
            if (this._timeElapsed >= this._queueBuffer)
            {
                this._canQueue = true;
            }
        }

        /*if (_canQueue && _moveComp.WantsAttack())
        {
            GD.Print("QUEUED ATTACK, SETTING TO TRUE");
            BB.SetPrimVar(BBDataSig.QueuedNextAttack, true);
        }*/
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
    }

    #endregion
}
