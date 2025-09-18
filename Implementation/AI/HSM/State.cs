namespace Jmodot.Implementation.AI.HSM;

using System.Linq;
using BB;
using Core.AI.BB;
using Core.AI.BehaviorTree;
using Core.AI.HSM;

using GColl = Godot.Collections;
using System.Collections.Generic;

/// <summary>
/// The abstract base class for all states in the Hierarchical State Machine.
/// It manages the core lifecycle, blackboard interactions, and the new declarative transition system.
/// </summary>
[GlobalClass, Tool]
public abstract partial class State : Node, IState
{
    [Signal]
    public delegate void TransitionStateEventHandler(State oldState, State newState);

    [Signal]
    public delegate void AddParallelStateEventHandler(State parallelState);

    [Signal]
    public delegate void RemoveParallelStateEventHandler(State parallelState);

    // Explicit interface implementation for events
    event TransitionStateEventHandler IState.TransitionState
    {
        add => TransitionState += value;
        remove => TransitionState -= value;
    }

    event AddParallelStateEventHandler IState.AddParallelState
    {
        add => AddParallelState += value;
        remove => AddParallelState -= value;
    }

    event RemoveParallelStateEventHandler IState.RemoveParallelState
    {
        add => RemoveParallelState += value;
        remove => RemoveParallelState -= value;
    }

    /// <summary>
    /// A list of potential transitions from this state. In each process frame,
    /// the state will check these transitions in order and execute the first one whose conditions are met.
    /// </summary>
    [Export]
    protected GColl.Array<StateTransition> Transitions { get; private set; } = new();

    /// <summary>
    /// Modifies the agent's 'SelfInterruptible' property on the blackboard when this state is active.
    /// </summary>
    [Export] protected InterruptibleChange SelfInteruptible = InterruptibleChange.NoChange;

    public IBlackboard BB { get; protected set; }
    public Node Agent { get; protected set; }
    public bool IsInitialized { get; protected set; }

    protected Dictionary<State, bool> ParallelStates { get; private set; } = new();

    public virtual void Init(Node agent, IBlackboard bb)
    {
        Agent = agent;
        BB = bb;

        foreach (var transition in Transitions.Where(t => t.IsValid()))
        {
            foreach (var condition in transition.Conditions.Where(c => c.IsValid()))
            {
                condition.Init(agent, bb);
            }
        }

        IsInitialized = true;
    }

    /// <summary>
    /// Template Method: Enters the state. Do not override. Override OnEnter for custom logic.
    /// </summary>
    public void Enter(Dictionary<State, bool> parallelStates)
    {
        this.ParallelStates = parallelStates;
        switch (SelfInteruptible)
        {
            case InterruptibleChange.True:
                BB.SetPrimVar(BBDataSig.SelfInteruptible, true); break;
            case InterruptibleChange.False:
                BB.SetPrimVar(BBDataSig.SelfInteruptible, false); break;
        }

        OnEnter();
    }

    /// <summary>
    /// Template Method: Exits the state. Do not override. Override OnExit for custom logic.
    /// </summary>
    public void Exit()
    {
        OnExit();
    }

    /// <summary>
    /// Template Method: Per-frame processing. Do not override. Override OnProcessFrame for custom logic.
    /// </summary>
    public void ProcessFrame(float delta)
    {
        OnProcessFrame(delta);
        CheckTransitions();
    }

    /// <summary>
    /// Template Method: Per-physics-frame processing. Do not override. Override OnProcessPhysics for custom logic.
    /// </summary>
    public void ProcessPhysics(float delta)
    {
        OnProcessPhysics(delta);
    }

    /// <summary>
    /// Template Method: Handles input. Do not override. Override OnHandleInput for custom logic.
    /// </summary>
    public void HandleInput(InputEvent @event)
    {
        OnHandleInput(@event);
    }

    /// <summary>
    /// Override to implement custom state entry logic.
    /// </summary>
    protected virtual void OnEnter()
    {
    }

    /// <summary>
    /// Override to implement custom state exit logic.
    /// </summary>
    protected virtual void OnExit()
    {
    }

    /// <summary>
    /// Override to implement custom per-frame logic.
    /// </summary>
    protected virtual void OnProcessFrame(float delta)
    {
    }

    /// <summary>
    /// Override to implement custom per-physics-frame logic.
    /// </summary>
    protected virtual void OnProcessPhysics(float delta)
    {
    }

    /// <summary>
    /// Override to implement custom input handling logic.
    /// </summary>
    protected virtual void OnHandleInput(InputEvent @event)
    {
    }

    /// <summary>
    /// Iterates through the exported transitions and triggers the first one whose conditions are met.
    /// </summary>
    private void CheckTransitions()
    {
        foreach (var transition in Transitions.Where(t => t.IsValid() && t.TargetState.IsValid()))
        {
            // A transition with no conditions is not checked automatically.
            // It must be triggered manually via EmitSignal.
            if (transition.Conditions.Count == 0) continue;

            bool allConditionsMet = transition.Conditions
                .Where(c => c.IsValid())
                .All(c => c.Check());

            if (allConditionsMet)
            {
                EmitSignal(SignalName.TransitionState, this, transition.TargetState);
                // Stop after the first valid transition is found.
                return;
            }
        }
    }
}
