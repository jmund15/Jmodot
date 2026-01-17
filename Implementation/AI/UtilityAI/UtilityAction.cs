// --- UtilityAction.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using System.Collections.Generic;
using System.Linq;
using Godot;
using JmoAI.UtilityAI;
using Core.AI.BB;
using BehaviorTree.Tasks;

/// <summary>
/// Base action for all utility-based behaviors. Implements IUtilityTask for use with UtilitySelector.
/// Supports configurable interruptibility windows.
/// </summary>
[GlobalClass, Tool]
public partial class UtilityAction : BehaviorAction, IUtilityTask
{
    #region TASK_VARIABLES
    [Export]
    public UtilityConsideration? Consideration { get; set; }

    /// <summary>
    /// Time in seconds before this action can be interrupted. -1 means never interruptible.
    /// </summary>
    [Export(PropertyHint.Range, "-1,100,1")]
    public float NonInterruptibleTime { get; protected set; } = 0.25f;

    [Export]
    public int Priority { get; private set; } = 0;

    public bool Interruptible { get; private set; } = true;

    protected override void OnEnter()
    {
        base.OnEnter();
        if (NonInterruptibleTime < 0)
        {
            Interruptible = false;
        }
        else if (NonInterruptibleTime > 0)
        {
            Interruptible = false;
            GetTree().CreateTimer(NonInterruptibleTime).Timeout += () => Interruptible = true;
        }
        else
        {
            Interruptible = true;
        }
    }

    protected override void OnExit()
    {
        base.OnExit();
        // Reset interruptibility for next entry
        Interruptible = true;
    }

    protected override void OnProcessFrame(float delta)
    {
        base.OnProcessFrame(delta);
    }

    protected override void OnProcessPhysics(float delta)
    {
        base.OnProcessPhysics(delta);
    }
    #endregion

    #region TASK_HELPER
    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (Consideration == null)
        {
            warnings.Add("UtilityAction requires a Consideration to evaluate its utility score.");
        }
        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }
    #endregion
}
