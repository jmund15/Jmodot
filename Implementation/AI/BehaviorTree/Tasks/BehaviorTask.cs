#region

using System.Collections.Generic;
using Godot.Collections;
using Jmodot.Core.AI.BB;
using BTaskStatus = Jmodot.Implementation.AI.BehaviorTree.Tasks.BTaskStatus;

#endregion

[GlobalClass]
[Tool]
public partial class BehaviorTask : Node
{
    #region TASK_VARIABLES

    public string TaskName { get; protected set; }
    public Node Agent { get; private set; }
    public IBlackboard BB { get; private set; }
    private BTaskStatus _status;

    public BTaskStatus Status
    {
        get => this._status;
        set
        {
            if (this._status == value) return;
            //Global.LogError($"STATUS CHANGED ON: {Name}");
            this._status = value;
            this.EmitSignal(SignalName.TaskStatusChanged, Variant.From(this._status));
        }
    }

    [Export] public Array<BTCondition> Conditions { get; private set; } = new();

    [Signal]
    public delegate void TaskStatusChangedEventHandler(BTaskStatus newStatus);

    #endregion

    #region TASK_UPDATES

    public virtual void Init(Node agent, IBlackboard bb)
    {
        this.Agent = agent;
        this.BB = bb;
        this.Status = BTaskStatus.FRESH;
        this.TaskName += this.Name;
        foreach (var condition in this.Conditions)
        {
            condition.Init(agent, bb);
            this.TaskName += condition.ConditionName;
        }
        //GD.Print("INIT TASK: ", TaskName);
    }

    public virtual void Enter()
    {
        this.Status = BTaskStatus.FRESH;
        //Status = BTaskStatus.RUNNING;
        //TODO: make sure is ok? currently deferring to allow proper entering and exiting of tasks.
        //But if conditions fail, do you really want them to even enter?
        //Solution could be for enter to return a enum (Enter_success, enter_failure, enter_running)?
        this.CallDeferred(MethodName.EnterConditions);
        //GD.Print($"Task {TaskName} entered");
    }

    public virtual void Exit()
    {
        //GD.Print($"Task {TaskName} exited with status {Status}");
        this.CallDeferred(MethodName.ExitConditions);
    }

    public virtual void ProcessFrame(float delta)
    {
        // Status = BTaskStatus.RUNNING;
        //GD.Print("Processing frame for task: ", TaskName);
        foreach (var condition in this.Conditions)
            //GD.Print("RUNNING CONDITION FOR: ", condition.ConditionName);
            condition.ProcessFrame(delta);
    }

    public virtual void ProcessPhysics(float delta)
    {
        // Status = BTaskStatus.RUNNING;
        foreach (var condition in this.Conditions) condition.ProcessPhysics(delta);
    }

    #endregion

    #region TASK_HELPER

    private void EnterConditions()
    {
        foreach (var condition in this.Conditions)
        {
            condition.ExitTaskEvent += this.OnConditionExit;
            if (this.Status != BTaskStatus.FRESH) continue;
            condition.Enter();
            GD.Print("entered condition: ", condition.ConditionName);
        }

        if (this.Status == BTaskStatus.FRESH) // if status didn't exit from conditions, change to running
        {
            GD.Print("After entering conditions, still at fresh, so running now.");
            this.Status = BTaskStatus.RUNNING;
        }
    }

    private void ExitConditions()
    {
        foreach (var condition in this.Conditions)
        {
            condition.ExitTaskEvent -= this.OnConditionExit;
            condition.Exit();
        }
    }

    private void OnConditionExit(object sender, bool succeedTask)
    {
        //if (!Conditions.Contains(sender)) { return; } //safeguard, may be slow and unecessary tho

        if (succeedTask)
            this.Status = BTaskStatus.SUCCESS;
        else
            this.Status = BTaskStatus.FAILURE;
        GD.Print($"EXITED TASK ON {this.Status} DUE TO CONDITION: {((BTCondition)sender).ConditionName}");
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        //if (GetChildren().Any(x => x is not BehaviorTask)) {
        //    warnings.Add("All children of this node should inherit from BehaviorTask class.");
        //}

        return warnings.ToArray();
    }

    #endregion
}
