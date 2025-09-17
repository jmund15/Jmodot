#region

using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BehaviorTree.Tasks;

#endregion

namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

[GlobalClass]
[Tool]
public partial class Selector : CompositeTask
{
    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        TaskName += "_Selector";
    }

    public override void Enter()
    {
        base.Enter();
        RunningChildIdx = 0;
        RunningChild = ChildTasks[RunningChildIdx];
        RunningChild.Enter();
        GD.Print($"{TaskName} & child {RunningChild.TaskName} entered");
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
                Status = BTaskStatus.SUCCESS;
                break;
            case BTaskStatus.FAILURE:
                RunningChildIdx++;
                if (RunningChildIdx == ChildTasks.Count)
                {
                    // failed to complete any child tasks in sequence
                    Status = BTaskStatus.FAILURE;
                }
                else
                {
                    // go to next task
                    RunningChild.Exit();
                    RunningChild = ChildTasks[RunningChildIdx];
                    RunningChild.Enter();
                }

                break;
            case BTaskStatus.RUNNING:
                Status = newStatus;
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