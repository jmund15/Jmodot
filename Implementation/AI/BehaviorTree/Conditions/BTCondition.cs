#region

using System;
using Jmodot.Core.AI.BB;

#endregion

//public enum ConditionStatus
//{
//	SuceedTask,
//	RunTask,
//	FailTask
//}

[GlobalClass]
[Tool]
public abstract partial class BTCondition : Resource
{
    #region TASK_HELPER

    protected virtual void OnExitTask()
    {
        if (this.SucceedTaskOnExit)
        {
            this.ExitTaskEvent?.Invoke(this, true);
            return;
        }

        this.ExitTaskEvent?.Invoke(this, false);
    }

    #endregion

    #region TASK_VARIABLES

    public string ConditionName { get; protected set; }

    protected Node Agent;
    protected IBlackboard BB;

    [Export] protected bool SucceedTaskOnExit; // naturally all tasks will fail on condition exit

    //private BTaskStatus _status = BTaskStatus.RUNNING; // all conditions are successfull until proved failure
    //public BTaskStatus Status {
    //       get => _status;
    //       set
    //       {
    //           if (_status == value) { return; }
    //           _status = value;
    //		ConditionStatusChangedEvent?.Invoke(this, _status);
    //           //EmitSignal(SignalName.ConditionStatusChanged, Variant.From(_status));
    //       }
    //   }

    //public event EventHandler<BTaskStatus> ConditionStatusChangedEvent;

    public event EventHandler<bool> ExitTaskEvent;

    #endregion

    #region TASK_UPDATES

    public virtual void Init(Node agent, IBlackboard bb)
    {
        this.Agent = agent;
        this.BB = bb;
    }

    public virtual void Enter()
    {
        //GD.Print($"Entered Decorator '{ConditionName}'.");
    }

    public virtual void Exit()
    {
    }

    public virtual void ProcessFrame(float delta)
    {
    }

    public virtual void ProcessPhysics(float delta)
    {
    }

    #endregion
}
