namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Core.AI.HSM;
using Implementation.Shared;
using Jmodot.Core.Shared.Attributes;

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
    [Export, RequiredExport] public StringName BBSignature { get; private set; } = null!;
    [Export] public bool Value { get; private set; }
    public override bool Check(Node agent, IBlackboard bb)
    {
        if (string.IsNullOrEmpty(BBSignature))
        {
            JmoLogger.Warning(this, "Can't check for transition condition, BBSignature is null or empty!");
            return false;
        }

        if (!bb.TryGet<bool>(BBSignature, out var bbVal))
        {
            JmoLogger.Warning(this, $"BB key '{BBSignature}' not found — defaulting to false.");
            return false;
        }

        return bbVal == Value;
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
