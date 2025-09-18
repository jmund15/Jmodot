namespace Jmodot.Core.AI.BehaviorTree;

using Godot;
using BB;

/// <summary>
/// Defines the public contract for any task within a Behavior Tree.
/// </summary>
public interface IBehaviorTask
{
    /// <summary>
    /// Gets the current execution status of the task.
    /// </summary>
    TaskStatus Status { get; }

    /// <summary>
    /// Initializes the task with the agent and blackboard context. Called once when the tree is set up.
    /// </summary>
    /// <param name="agent">The Node executing the behavior.</param>
    /// <param name="bb">The blackboard for shared data.</param>
    void Init(Node agent, IBlackboard bb);

    /// <summary>
    /// Starts the execution of the task.
    /// </summary>
    void Enter();

    /// <summary>
    /// Stops the execution of the task and cleans up its state.
    /// </summary>
    void Exit();

    /// <summary>
    /// Executes the task's logic for a single game frame.
    /// </summary>
    /// <param name="delta">The time elapsed since the last frame.</param>
    void ProcessFrame(float delta);

    /// <summary>
    /// Executes the task's logic for a single physics frame.
    /// </summary>
    /// <param name="delta">The time elapsed since the last physics frame.</param>
    void ProcessPhysics(float delta);
}
