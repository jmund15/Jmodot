namespace Jmodot.Implementation.AI.HSM;

using System.Linq;
using BB;
using Core.AI.BB;
using Core.AI.BehaviorTree;
using Core.AI.HSM;

using GColl = Godot.Collections;
using System.Collections.Generic;
using Shared;

/// <summary>
/// The abstract base class for all states in the Hierarchical State Machine.
/// It manages the core lifecycle, blackboard interactions, and the new declarative transition system.
/// </summary>
[GlobalClass, Tool]
public partial class State : Node, IState
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

    protected List<StateTransition> UniqueTransitions { get; private set; } = new();

    /// <summary>
    /// Modifies the agent's 'SelfInterruptible' property on the blackboard when this state is active.
    /// </summary>
    [Export] protected InterruptibleChange SelfInteruptible = InterruptibleChange.NoChange;

    public IBlackboard BB { get; protected set; }
    public Node Agent { get; protected set; }
    public bool IsInitialized { get; protected set; }

    protected Dictionary<State, bool> ParallelStates { get; private set; } = new();

    public void Init(Node agent, IBlackboard bb)
    {
        Agent = agent;
        BB = bb;

        // OLD
        // manually deep duplicate every transition (and therefore condition)
        // otherwise instantiated scenes will share the same resource, which will cripple transition logic
        // transition.ResourceLocalToScene = true;
        // foreach (var condition in transition.Conditions)
        // {
        //     condition.ResourceLocalToScene = true;
        // }
        //var dupedTransition = (StateTransition)transition.DuplicateDeep(Resource.DeepDuplicateMode.Internal); // TODO: ensure this is the correct duplicate mode! may need 'All'
        // foreach (var condition in dupedTransition.Conditions.Where(c => c.IsValid()))
        // {
        //     condition.Init(agent, bb);
        // }
        // UniqueTransitions.Add(dupedTransition!);

        // --- IMPORTANT ---
        // Transitions are now stateless resources, so we don't need to duplicate them.
        // We just add them to the UniqueTransitions list (or just use Transitions directly if UniqueTransitions is redundant).
        // For now, let's populate UniqueTransitions to keep the rest of the code working.
        foreach (var transition in Transitions.Where(t => t.IsValid()))
        {
            UniqueTransitions.Add(transition);
        }

        OnInit();

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
                BB.Set(BBDataSig.SelfInteruptible, true); break;
            case InterruptibleChange.False:
                BB.Set(BBDataSig.SelfInteruptible, false); break;
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

    // DEPRECATED
    // /// <summary>
    // /// Template Method: Handles input. Do not override. Override OnHandleInput for custom logic.
    // /// </summary>
    // public void HandleInput(InputEvent @event)
    // {
    //     OnHandleInput(@event);
    // }

    /// <summary>
    /// Hook for custom initialization logic. Called by Init().
    /// </summary>
    protected virtual void OnInit() { }

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

    // DEPRECATED
    // /// <summary>
    // /// Override to implement custom input handling logic.
    // /// </summary>
    // protected virtual void OnHandleInput(InputEvent @event)
    // {
    // }

    /// <summary>
    /// Iterates through the exported transitions, resolves their NodePaths,
    /// and triggers the first one whose conditions are met.
    /// </summary>
    private void CheckTransitions()
    {
        foreach (var transition in UniqueTransitions)
        {
            if (!transition.IsValid())
            {
                continue;
            }

            // A transition with an empty path is invalid.
            if (transition.TargetStatePath == null || transition.TargetStatePath.IsEmpty)
            {
                continue;
            }

            // A transition with no conditions is not checked automatically.
            // It must be triggered manually by emitting the TransitionState signal from state logic.
            if (transition.Conditions.Count == 0)
            {
                continue;
            }

            bool allConditionsMet = transition.Conditions
                .Where(c => c.IsValid())
                .All(c => c.Check(Agent, BB));


            if (allConditionsMet)
            {
                // Resolve the NodePath to get the actual State node instance.
                var targetState = GetNode<State>(transition.TargetStatePath);

                if (!targetState.IsValid())
                {
                    JmoLogger.Error(this, $"Transition condition met, but NodePath '{transition.TargetStatePath}' did not resolve to a valid State node.");
                    continue; // Try the next transition
                }

                EmitSignal(SignalName.TransitionState, this, targetState);
                return; // Stop after the first valid transition is found.
            }
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        for (int i = 0; i < Transitions.Count; i++)
        {
            var transition = Transitions[i];
            if (transition == null)
            {
                warnings.Add($"Transition at index {i} is not assigned.");
                continue;
            }

            var path = transition.TargetStatePath;
            if (path == null || path.IsEmpty)
            {
                // This is already warned about in the resource, but good to have here too.
                warnings.Add($"Transition '{transition.ResourceName}' (index {i}) has no TargetStatePath assigned.");
                continue;
            }

            // The State node, unlike the resource, has a scene context and can validate the path.
            if (!HasNode(path))
            {
                warnings.Add($"Transition '{transition.ResourceName}' (index {i}) has an invalid TargetStatePath: '{path}'. The node was not found.");
            }
            else if (GetNode(path) is not State)
            {
                warnings.Add($"Transition '{transition.ResourceName}' (index {i}) points to a node ('{path}') that is not a valid State.");
            }
        }

        return warnings.ToArray();
    }
}
