namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BehaviorTree.Conditions;
using Implementation.Shared;

/// <summary>
/// A BT condition that checks a boolean value on the blackboard.
/// Returns true when the BB value matches the expected Value export.
///
/// Use as a guard on BT branches: e.g., ScurrySequence guarded by
/// BBBoolBTCondition(key="Critter_Threatened", value=true).
/// </summary>
[GlobalClass, Tool]
public partial class BBBoolBTCondition : BTCondition
{
    /// <summary>
    /// The BB key to check.
    /// </summary>
    [Export]
    public StringName BBKey { get; private set; } = new("");

    /// <summary>
    /// The expected value. Condition passes when BB value matches this.
    /// </summary>
    [Export]
    public bool Value { get; private set; } = true;

    public override bool Check()
    {
        if (string.IsNullOrEmpty(BBKey))
        {
            JmoLogger.Warning(this, "BBBoolBTCondition: BBKey is null or empty.");
            return false;
        }

        if (!BB.TryGet<bool>(BBKey, out var bbVal))
        {
            // Key not set — conservative default: condition fails
            return false;
        }

        return bbVal == Value;
    }
}
