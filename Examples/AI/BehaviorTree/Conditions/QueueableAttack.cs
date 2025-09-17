#region

using Jmodot.Core.AI.BB;
using Jmodot.Core.Movement;
using Jmodot.Implementation.AI.BB;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

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
        _queueBuffer = 0f;
        //_setQueueFalseOnEnter = false;
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        _moveComp = BB.GetVar<ICharacterController3D>(BBDataSig.MoveComp);
        ConditionName = $"_QueueableAttack:{_queueBuffer}";
    }

    public override void Enter()
    {
        base.Enter();
        //if (_setQueueFalseOnEnter)
        //{
        //          BB.SetPrimVar(BBDataSig.QueuedNextAttack, false);
        //      }
        _timeElapsed = 0f;
        if (_queueBuffer <= 0f) _canQueue = true;
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
        if (!_canQueue)
        {
            _timeElapsed += delta;
            if (_timeElapsed >= _queueBuffer) _canQueue = true;
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