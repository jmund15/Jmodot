namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Tasks;

[GlobalClass]
[Tool]
public abstract partial class CompositeTask : BehaviorTask
{
    #region TASK_VARIABLES

    public BehaviorTask _runningChild;

    public BehaviorTask RunningChild
    {
        get => this._runningChild;
        set
        {
            if (this._runningChild == value)
            {
                return;
            }

            this._runningChild = value;
            this.RunningChildChanged?.Invoke(this, this._runningChild);
        }
    }

    public int RunningChildIdx { get; set; }
    public List<BehaviorTask> ChildTasks { get; protected set; } = new();

    public event EventHandler<BehaviorTask> RunningChildChanged;

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        foreach (var cbt in this.GetChildrenOfType<BehaviorTask>(false))
        {
            this.ChildTasks.Add(cbt);
            cbt.Init(agent, bb);
            cbt.TaskStatusChanged += this.OnRunningChildStatusChange;
        }
        //GD.PrintErr($"Composite task {Name} has {ChildTasks.Count} children!");
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
        this.RunningChild.IfValid()?.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
        if (this.Status == BTaskStatus.RUNNING)
        {
            this.RunningChild.ProcessFrame(delta);
        }
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
        if (this.Status == BTaskStatus.RUNNING)
        {
            this.RunningChild.ProcessPhysics(delta);
        }
    }

    #endregion

    #region TASK_HELPER

    protected virtual void OnRunningChildStatusChange(BTaskStatus newStatus)
    {
        //GD.Print($"{TaskName}'s child {RunningChild.TaskName} status changed to {newStatus}");
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (this.GetChildren().Count == 0)
        {
            warnings.Add("Composite Task must have a child!");
        }

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #endregion
}
