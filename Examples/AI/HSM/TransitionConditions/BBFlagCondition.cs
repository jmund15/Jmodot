namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Core.AI.HSM;
using Core.Shared.Attributes;
using Implementation.Shared;

/// <summary>
/// A transition condition that checks for a boolean "flag" on the blackboard.
/// The flag is consumed (set to false) only when the transition commits via
/// <see cref="OnTransitionCommitted"/>, ensuring the flag survives if CanExit() blocks.
/// </summary>
[GlobalClass]
public partial class BBFlagCondition : TransitionCondition
{
    /// <summary>
    /// The key/name of the boolean variable on the blackboard to check.
    /// </summary>
    [Export, RequiredExport]
    public StringName BBFlagSignature { get; private set; } = null!;

    /// <summary>
    /// Pure read — returns true if the flag is set, false otherwise.
    /// Does NOT consume the flag. Consumption is deferred to OnTransitionCommitted.
    /// </summary>
    public override bool Check(Node agent, IBlackboard bb)
    {
        if (string.IsNullOrEmpty(BBFlagSignature))
        {
            JmoLogger.Warning(this, $"Can't check for transition condition, BBSignature is null or empty!");
            return false;
        }

        if (!bb.TryGet<bool>(BBFlagSignature, out var flag))
        {
            return false;
        }
        return flag;
    }

    /// <summary>
    /// Consumes the flag after the transition has fully committed.
    /// </summary>
    public override void OnTransitionCommitted(Node agent, IBlackboard bb)
    {
        if (string.IsNullOrEmpty(BBFlagSignature)) { return; }

        if (bb.TryGet<bool>(BBFlagSignature, out var flag) && flag)
        {
            bb.Set(BBFlagSignature, false);
        }
    }

    // TODO: make this into an interface that has this function. nodes with config warnings will call this and add to their warnings
    public string[] GetResourceConfigurationWarnings()
    {
        if (string.IsNullOrEmpty(BBFlagSignature))
        {
            return new[] { "'Blackboard Flag Name' cannot be empty." };
        }
        return [];
    }

    #region Test Helpers
#if TOOLS
    internal void SetBBFlagSignature(StringName value) => BBFlagSignature = value;
#endif
    #endregion
}
