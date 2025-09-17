#region

using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BehaviorTree.Composites;

#endregion

namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

[GlobalClass]
[Tool]
public abstract partial class Decorator : CompositeTask
{
    #region TASK_HELPER

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (this.GetChildrenOfType<BehaviorAction>().Count > 1)
            warnings.Add("Decorators should have exactly one Task child!");

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        TaskName += "_Decorator";
        RunningChild = this.GetFirstChildOfType<BehaviorTask>(false);
    }

    public override void Enter()
    {
        base.Enter();
        RunningChild.Enter();
    }

    public override void Exit()
    {
        base.Exit();
        RunningChild.Exit();
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