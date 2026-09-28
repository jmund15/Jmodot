// --- UtilityAction.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.AI.BB;
using BehaviorTree.Tasks;
using Jmodot.Implementation.Shared;

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

    private long _activation;

    protected override void OnEnter()
    {
        base.OnEnter();
        var activation = ++_activation;
        if (NonInterruptibleTime < 0)
        {
            Interruptible = false;
        }
        else if (NonInterruptibleTime > 0)
        {
            Interruptible = false;
            GameClock.CreateTimer(this, NonInterruptibleTime).Timeout += () =>
            {
                if (activation == _activation) { Interruptible = true; }
            };
        }
        else
        {
            Interruptible = true;
        }
    }

    protected override void OnExit()
    {
        ++_activation;
        base.OnExit();
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
