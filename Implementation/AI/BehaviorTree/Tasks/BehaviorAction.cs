#region

using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.AI.BB;
using Jmodot.Core.AI.BehaviorTree;
using Jmodot.Implementation.AI.BB;

#endregion

[GlobalClass]
[Tool]
public partial class BehaviorAction : BehaviorTask
{
    #region TASK_VARIABLES

    [Export] protected InterruptibleChange SelfInterruptible = InterruptibleChange.NoChange;

    #endregion

    #region TASK_HELPER

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (this.GetChildrenOfType<BehaviorTask>().Count > 0)
            warnings.Add("BehaviorAction must be a leaf node (NO BehaviorTask children)");

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #endregion

    #region TASK_UPDATES

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        TaskName += "_Action";
    }

    public override void Enter()
    {
        base.Enter();
        switch (SelfInterruptible)
        {
            case InterruptibleChange.NoChange:
                break;
            case InterruptibleChange.True:
                BB.SetPrimVar(BBDataSig.SelfInteruptible, true); break;
            case InterruptibleChange.False:
                BB.SetPrimVar(BBDataSig.SelfInteruptible, false); break;
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void ProcessFrame(float delta)
    {
        base.ProcessFrame(delta);
    }

    public override void ProcessPhysics(float delta)
    {
        base.ProcessPhysics(delta);
    }

    #endregion
}