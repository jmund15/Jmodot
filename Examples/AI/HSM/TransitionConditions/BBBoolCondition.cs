namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.HSM;
using Implementation.Shared;

/// <summary>
/// A transition condition that checks for a variable within the blackboard.
/// If equal to the given value, the condition returns true
/// </summary>
[GlobalClass]
public partial class BBBoolCondition : TransitionCondition
{
    /// <summary>
    /// The key/name of the variable on the blackboard to check.
    /// </summary>
    [Export] public StringName BBSignature { get; private set; } = null!;
    [Export] public bool Value { get; private set; }
    protected override void OnInit()
    {
        base.OnInit();
    }

    public override bool Check()
    {
        if (string.IsNullOrEmpty(BBSignature))
        {
            JmoLogger.Warning(this, $"Can't check for transition condition, BBSignature is null or empty!");
            return false;
        }

        // Get the boolean variable from the blackboard.
        var bbVal = BB.Get<bool>(BBSignature);

        if (bbVal == null)
        {
            JmoLogger.Warning(this, $"Can't check for transition condition, the blackboard value for {BBSignature} is not of type bool.");
            return false;
        }

        // Check if the flag exists and is true.
        if (bbVal == Value)
        {
            // Allow the transition.
            return true;
        }

        // Do not transition.
        return false;
    }

    // TODO: make this into an interface that has this function. nodes with config warnings will call this and add to their warnings
    public string[] GetResourceConfigurationWarnings()
    {
        if (string.IsNullOrEmpty(BBSignature))
        {
            return new[] { "'Blackboard Flag Name' cannot be empty." };
        }
        return [];
    }
}
