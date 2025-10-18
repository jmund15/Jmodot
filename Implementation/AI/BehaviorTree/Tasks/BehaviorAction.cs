namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using BB;
using Core.AI.BehaviorTree;

/// <summary>
/// Represents a leaf node in the Behavior Tree. Actions are where the actual work
/// (e.g., moving, attacking, playing an animation) is performed.
/// </summary>
[GlobalClass, Tool]
public abstract partial class BehaviorAction : BehaviorTask
{
    /// <summary>
    /// Modifies the agent's 'SelfInterruptible' property on the blackboard when this task is active.
    /// </summary>
    [Export] protected InterruptibleChange SelfInterruptible = InterruptibleChange.NoChange;

    protected override void OnEnter()
    {
        base.OnEnter();
        switch (this.SelfInterruptible)
        {
            case InterruptibleChange.True:
                this.BB.Set(BBDataSig.SelfInteruptible, true);
                break;
            case InterruptibleChange.False:
                this.BB.Set(BBDataSig.SelfInteruptible, false);
                break;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (this.GetChildren().OfType<BehaviorTask>().Any())
        {
            warnings.Add("BehaviorAction must be a leaf node and cannot have BehaviorTask children.");
        }
        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }
}
