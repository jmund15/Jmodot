namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Tasks;

[GlobalClass]
[Tool]
public partial class Selector : CompositeTask
{
    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        this.TaskName += "_Selector";
    }

    public override void Enter()
    {
        base.Enter();
        this.RunningChildIdx = 0;
        this.RunningChild = this.ChildTasks[this.RunningChildIdx];
        this.RunningChild.Enter();
        GD.Print($"{this.TaskName} & child {this.RunningChild.TaskName} entered");
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
        switch (newStatus)
        {
            case BTaskStatus.SUCCESS:
                //RunningChild.Exit();
                this.Status = BTaskStatus.SUCCESS;
                break;
            case BTaskStatus.FAILURE:
                this.RunningChildIdx++;
                if (this.RunningChildIdx == this.ChildTasks.Count)
                {
                    // failed to complete any child tasks in sequence
                    this.Status = BTaskStatus.FAILURE;
                }
                else
                {
                    // go to next task
                    this.RunningChild.Exit();
                    this.RunningChild = this.ChildTasks[this.RunningChildIdx];
                    this.RunningChild.Enter();
                }

                break;
            case BTaskStatus.RUNNING:
                this.Status = newStatus;
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
