namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Implementation.AI.BehaviorTree.Composites;

[GlobalClass]
[Tool]
public abstract partial class Decorator : CompositeTask
{
    #region TASK_HELPER

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (this.GetChildrenOfType<BehaviorAction>().Count > 1)
        {
            warnings.Add("Decorators should have exactly one Task child!");
        }

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        this.TaskName += "_Decorator";
        this.RunningChild = this.GetFirstChildOfType<BehaviorTask>(false);
    }

    public override void Enter()
    {
        base.Enter();
        this.RunningChild.Enter();
    }

    public override void Exit()
    {
        base.Exit();
        this.RunningChild.Exit();
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
}
