namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.HSM;

/// <summary>
/// A transition condition that checks for a boolean "flag" on the blackboard.
/// Crucially, it consumes the flag (sets it to false) immediately after a successful check,
/// ensuring the transition is only triggered once per event.
/// </summary>
[GlobalClass]
public partial class FlagTransitionCondition : TransitionCondition
{
    /// <summary>
    /// The key/name of the boolean variable on the blackboard to check.
    /// </summary>
    [Export]
    public string BlackboardFlagName { get; private set; }

    public override bool Check()
    {
        if (string.IsNullOrEmpty(BlackboardFlagName))
        {
            return false;
        }

        // Get the primitive variable from the blackboard.
        var flag = BB.GetPrimVar<bool>(BlackboardFlagName);

        // Check if the flag exists and is true.
        if (flag != null && flag.Value)
        {
            // The flag is set. Consume it immediately.
            BB.SetPrimVar(BlackboardFlagName, false);

            // Allow the transition.
            return true;
        }

        // The flag was not set, do not transition.
        return false;
    }

    // TODO: make this into an interface that has this function. nodes with config warnings will call this and add to their warnings
    public string[] GetResourceConfigurationWarnings()
    {
        if (string.IsNullOrEmpty(BlackboardFlagName))
        {
            return new[] { "'Blackboard Flag Name' cannot be empty." };
        }
        return [];
    }
}
