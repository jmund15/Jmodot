#region

using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BehaviorTree.Composites;
using Jmodot.Implementation.AI.BehaviorTree.Tasks;

#endregion

[GlobalClass]
[Tool]
public partial class Sequence : CompositeTask
{
    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        this.TaskName += "_Sequence";
    }

    public override void Enter()
    {
        base.Enter();
        this.RunningChildIdx = 0;
        this.RunningChild = this.ChildTasks[this.RunningChildIdx];
        this.RunningChild.Enter();
        //GD.Print($"{TaskName} & child {RunningChild.TaskName} entered");
    }

    public override void Exit()
    {
        base.Exit();
        //RunningChild.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
        //RunningChild.ProcessFrame(delta);
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
        //RunningChild.ProcessPhysics(delta);
    }

    #endregion

    #region TASK_HELPER

    protected override void OnRunningChildStatusChange(BTaskStatus newStatus)
    {
        base.OnRunningChildStatusChange(newStatus);
        GD.Print($"sequence child {this.RunningChild.Name} status changed to {newStatus}");
        switch (newStatus)
        {
            case BTaskStatus.SUCCESS:
                this.RunningChildIdx++;
                if (this.RunningChildIdx == this.ChildTasks.Count)
                {
                    // successfully completed all child tasks in sequence
                    this.Status = BTaskStatus.SUCCESS;
                }
                else
                {
                    // go to next task
                    this.RunningChild.Exit();
                    this.RunningChild = this.ChildTasks[this.RunningChildIdx];
                    this.RunningChild.Enter();
                }

                break;
            case BTaskStatus.FAILURE:
                //RunningChild.Exit();
                this.Status = BTaskStatus.FAILURE;
                break;
            case BTaskStatus.RUNNING:
                this.Status = BTaskStatus.RUNNING;
                break;
            case BTaskStatus.FRESH:
                this.Status = BTaskStatus.RUNNING; //TODO: CONFIRM?
                break;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        //

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #endregion
}
