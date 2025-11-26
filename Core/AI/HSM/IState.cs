namespace Jmodot.Core.AI.HSM;

using System.Collections.Generic;
using BB;
using Implementation.AI.HSM;

/// <summary>
/// Defines the public contract for any state within a Hierarchical State Machine.
/// It outlines the lifecycle methods and signals required for state management and transitions.
/// </summary>
public interface IState
{
    /// <summary>
    /// Fired when this state determines a transition to a new state is required.
    /// The state machine controller (typically a CompoundState) listens for this.
    /// </summary>
    event State.TransitionStateEventHandler TransitionState;

    /// <summary>
    /// Fired when a new parallel state should be activated alongside the current primary state.
    /// </summary>
    event State.AddParallelStateEventHandler AddParallelState;

    /// <summary>
    /// Fired when an active parallel state should be deactivated.
    /// </summary>
    event State.RemoveParallelStateEventHandler RemoveParallelState;

    /// <summary>
    /// Initializes the state with the agent and blackboard context. Called once when the state machine is set up.
    /// </summary>
    /// <param name="agent">The Node executing the behavior.</param>
    /// <param name="bb">The blackboard for shared data.</param>
    void Init(Node agent, IBlackboard bb);

    /// <summary>
    /// Called when the state machine enters this state.
    /// </summary>
    /// <param name="parallelStates">A dictionary of all parallel states in the current context and their active status.</param>
    void Enter(Dictionary<State, bool> parallelStates);

    /// <summary>
    /// Called when the state machine exits this state.
    /// </summary>
    void Exit();

    /// <summary>
    /// Executes the state's logic for a single game frame.
    /// </summary>
    /// <param name="delta">The time elapsed since the last frame.</param>
    void ProcessFrame(float delta);

    /// <summary>
    /// Executes the state's logic for a single physics frame.
    /// </summary>
    /// <param name="delta">The time elapsed since the last physics frame.</param>
    void ProcessPhysics(float delta);

    // DEPRECATED
    // /// <summary>
    // /// Allows the state to process Godot input events.
    // /// </summary>
    // /// <param name="event">The input event to handle.</param>
    // void HandleInput(InputEvent @event);
}
