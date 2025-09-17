#region

using System;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BehaviorTree.Tasks;

#endregion

namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

[GlobalClass]
[Tool]
public abstract partial class CompositeTask : BehaviorTask
{
    #region TASK_VARIABLES

    public BehaviorTask _runningChild;

    public BehaviorTask RunningChild
    {
        get => _runningChild;
        set
        {
            if (_runningChild == value) return;
            _runningChild = value;
            RunningChildChanged?.Invoke(this, _runningChild);
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
            ChildTasks.Add(cbt);
            cbt.Init(agent, bb);
            cbt.TaskStatusChanged += OnRunningChildStatusChange;
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
        RunningChild.IfValid()?.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
        if (Status == BTaskStatus.RUNNING) RunningChild.ProcessFrame(delta);
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
        if (Status == BTaskStatus.RUNNING) RunningChild.ProcessPhysics(delta);
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

        if (GetChildren().Count == 0) warnings.Add("Composite Task must have a child!");

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #endregion
}