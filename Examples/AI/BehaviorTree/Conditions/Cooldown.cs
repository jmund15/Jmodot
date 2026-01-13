namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI;
using Core.AI.BB;
using Core.AI.BehaviorTree.Conditions;
using Implementation.AI.BehaviorTree.Tasks;
using Implementation.Shared;

/// <summary>
/// A stateful condition that manages a cooldown period for an action. It can be configured
/// to trigger the cooldown either immediately upon being checked, or only after its parent
/// task has completed successfully.
/// </summary>
[GlobalClass, Tool]
public partial class CooldownCondition : BTCondition
{
    public enum CooldownTrigger
    {
        /// <summary>The cooldown starts as soon as this condition is checked and passes. Ideal for instant, one-shot actions.</summary>
        OnCheck,
        /// <summary>The cooldown starts only after the parent BehaviorTask finishes with a SUCCESS status. Ideal for actions that have a duration.</summary>
        OnSuccess
    }

    [ExportGroup("Cooldown Settings")]
    [Export(PropertyHint.Range, "0.1, 60.0, 0.1, or_greater")]
    private float _cooldownDuration = 1.0f;

    [Export(PropertyHint.Enum)]
    private CooldownTrigger TriggerMode = CooldownTrigger.OnCheck;

    private bool _isReady = true;

    public override void Init(BehaviorTask owner, Node agent, IBlackboard bb)
    {
        base.Init(owner, agent, bb);
        // Ensure the state is fresh when the tree is initialized.
        _isReady = true;
    }

    public override void OnParentTaskEnter()
    {
        // If we need to listen for the success event, we subscribe when the parent enters.
        if (TriggerMode == CooldownTrigger.OnSuccess)
        {
            OwnerTask.TaskStatusChanged += OnParentStatusChanged;
        }
    }

    public override void OnParentTaskExit()
    {
        // Always unsubscribe on exit to prevent memory leaks.
        if (TriggerMode == CooldownTrigger.OnSuccess)
        {
            OwnerTask.TaskStatusChanged -= OnParentStatusChanged;
        }
    }

    /// <summary>
    /// Checks if the action is ready to be performed. Behavior depends on TriggerMode.
    /// </summary>
    public override bool Check()
    {
        if (!_isReady)
        {
            return false;
        }

        // If the mode is OnCheck, the successful check itself triggers the cooldown.
        if (TriggerMode == CooldownTrigger.OnCheck)
        {
            StartCooldown();
        }

        // The action is ready. If in OnSuccess mode, the cooldown will be
        // started by the OnParentStatusChanged event handler later.
        return true;
    }

    /// <summary>
    /// Listens for the parent task's status change to trigger the cooldown.
    /// </summary>
    private void OnParentStatusChanged(TaskStatus status)
    {
        // We only care about the task succeeding.
        if (status == TaskStatus.Success)
        {
            StartCooldown();
        }
    }

    /// <summary>
    /// Centralized method to begin the cooldown timer.
    /// </summary>
    private void StartCooldown()
    {
        if (!_isReady) { return; } // Prevent starting the cooldown multiple times.

        _isReady = false;
        JmoLogger.Info(this, $"Starting {_cooldownDuration}s cooldown.");

        // Use the Agent node to get the SceneTree.
        Agent.GetTree().CreateTimer(_cooldownDuration).Timeout += () =>
        {
            _isReady = true;
            JmoLogger.Info(this, "Cooldown finished. Ready.");
        };
    }
}
/*
 * Why passing the owner task is acceptable and correct here
 * The Behavior Tree architecture has a unique and very specific relationship
 * between its components that mitigates the dangers of this coupling and makes it beneficial.
 * A Symbiotic, Private Relationship: A BTCondition is not a general-purpose object.
 * Its entire existence is predicated on being attached to a BehaviorTask.
 * It is a decorator, an extension of the task's logic. It has no meaning or purpose on its own.
 * The relationship is not between two independent systems, but between a component and its dedicated sub-component.
 * Think of it less like two separate classes and more like an "externalized method" of the BehaviorTask.
 * The "Inversion of Control" Principle: The primary reason for the event-driven CooldownCondition
 * is to allow the condition to react to the task's result.
 * The alternative would be for the BehaviorTask to have special logic to handle this,
 * violates the Open/Closed Principle.
 * Every time you invent a new special condition, you have to modify the base BehaviorTask class.
 */
