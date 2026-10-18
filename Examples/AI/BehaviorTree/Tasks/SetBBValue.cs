namespace Jmodot.Examples.AI.BehaviorTree.Tasks;

using System.Collections.Generic;

using Core.AI;
using Implementation.AI.BehaviorTree.Tasks;

/// <summary>
/// An action that sets a value on the Blackboard. It succeeds immediately after setting the value.
/// Note: This is an ACTION, not a condition, as it changes state.
/// </summary>
[GlobalClass, Tool]
public partial class SetBBValue : BehaviorAction
{
    /// <summary>
    /// The blackboard key this step writes. Required: an empty key writes nothing and warns in the Inspector.
    /// </summary>
    [Export] public StringName Key { get; private set; }

    /// <summary>The value stored under <see cref="Key"/> when the step runs.</summary>
    [Export] public Variant Value { get; private set; }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>(base._GetConfigurationWarnings() ?? []);
        if (Key == null || Key.IsEmpty)
        {
            warnings.Add("Key is empty. Name the blackboard key this step writes.");
        }

        return warnings.ToArray();
    }

    protected override void OnEnter()
    {
        base.OnEnter();

        // Note: This does not distinguish between Set and Set.
        // A more robust implementation might have separate fields or use reflection,
        // but for a simple example, this works with Godot's Variant system.
        BB.Set(Key, Value); // Assuming primitive for simplicity. Use Set for objects.

        Status = TaskStatus.Success;
    }
}
