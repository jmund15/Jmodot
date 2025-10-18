namespace Jmodot.Examples.AI.BehaviorTree.Tasks;

using Core.AI;
using Implementation.AI.BehaviorTree.Tasks;

/// <summary>
/// An action that sets a value on the Blackboard. It succeeds immediately after setting the value.
/// Note: This is an ACTION, not a condition, as it changes state.
/// </summary>
[GlobalClass, Tool]
public partial class SetBBValue : BehaviorAction
{
    [Export] public StringName Key { get; private set; }
    [Export] public Variant Value { get; private set; }

    protected override void OnEnter()
    {
        base.OnEnter();

        // Note: This does not distinguish between Set and Set.
        // A more robust implementation might have separate fields or use reflection,
        // but for a simple example, this works with Godot's Variant system.
        BB.Set(Key, Value); // Assuming primitive for simplicity. Use Set for objects.

        Status = TaskStatus.SUCCESS;
    }
}
