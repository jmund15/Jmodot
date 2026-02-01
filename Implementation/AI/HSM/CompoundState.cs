namespace Jmodot.Implementation.AI.HSM;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.HSM;
using Shared;
using Shared.GodotExceptions;

/// <summary>
    /// A state that contains and manages a set of substates. It can have one primary
    /// substate active at a time, as well as multiple parallel substates. This is the
    /// core component for building hierarchical state machines.
    /// </summary>
    [GlobalClass, Tool]
    public partial class CompoundState : State
    {
        public enum DebugViewPosition
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        [Export]
        public State InitialSubState { get; protected set; }

        /// <summary>
        /// If false, when re-entering this CompoundState, it will resume the last active PrimarySubState
        /// instead of resetting to InitialSubState.
        /// </summary>
        [Export]
        public bool ResetsOnEntry { get; set; } = true;

        /// <summary>
        /// If true, a debug overlay will be instantiated to visualize the state machine's activity.
        /// This should typically only be enabled on the root-most CompoundState.
        /// </summary>
        [ExportGroup("Debugging")]
        [Export] private bool _enableDebugView = false;
        [Export] private DebugViewPosition _debugViewPosition = DebugViewPosition.TopRight;

        public State PrimarySubState { get; protected set; }

        public Dictionary<State, bool> FiniteSubStates { get; protected set; } = new();

        private DebugSMComponent _debugComponent;

        [Signal]
        public delegate void EnteredCompoundStateEventHandler();
        [Signal]
        public delegate void ExitedCompoundStateEventHandler();
        [Signal]
        public delegate void TransitionedSubStateEventHandler(State oldState, State newState);

        protected override void OnInit()
        {
            if (_enableDebugView && !Engine.IsEditorHint())
            {
                _debugComponent = new DebugSMComponent();
                AddChild(_debugComponent);
            }

            foreach (var child in this.GetChildrenOfType<State>(false))
            {
                child.Init(Agent, BB);
                child.TransitionState -= TransitionFiniteSubState; // Prevent double subscription
                child.TransitionState += TransitionFiniteSubState;
                if (!FiniteSubStates.ContainsKey(child))
                {
                    FiniteSubStates.Add(child, false);
                }
            }
        }

        protected override void OnEnter()
        {
            base.OnEnter();
            if (!InitialSubState.IsValid())
            {
                throw new NodeConfigurationException($"CompoundState '{Name}' has no valid InitialSubState assigned.", this);
            }

            // History State: If ResetsOnEntry is false and PrimarySubState is valid, resume it.
            if (!ResetsOnEntry && PrimarySubState.IsValid())
            {
                FiniteSubStates[PrimarySubState] = true;
                PrimarySubState.Enter();
            }
            else
            {
                PrimarySubState = InitialSubState;
                FiniteSubStates[PrimarySubState] = true;
                PrimarySubState.Enter();
            }

            EmitSignal(SignalName.EnteredCompoundState);
            _debugComponent?.OnEnteredCompoundState(PrimarySubState);
        }

        protected override void OnExit()
        {
            base.OnExit();
            PrimarySubState?.Exit();
            if(PrimarySubState.IsValid())
            {
                FiniteSubStates[PrimarySubState!] = false; // IsValid() guarantees non-null
            }
            // Note: We don't set PrimarySubState to null here to support History States.

            EmitSignal(SignalName.ExitedCompoundState);
            _debugComponent?.OnExitedCompoundState();
        }

        protected override void OnProcessFrame(float delta)
        {
            base.OnProcessFrame(delta);
            PrimarySubState?.ProcessFrame(delta);
        }

        protected override void OnProcessPhysics(float delta)
        {
            base.OnProcessPhysics(delta);
            PrimarySubState?.ProcessPhysics(delta);
        }

        // DEPRECATED
        // protected override void OnHandleInput(InputEvent @event)
        // {
        //     base.OnHandleInput(@event);
        //     PrimarySubState?.HandleInput(@event);
        //     foreach (var parallelState in ParallelSubStates.Where(ps => ps.Value))
        //     {
        //         parallelState.Key.HandleInput(@event);
        //     }
        // }

        public virtual void TransitionFiniteSubState(State oldSubState, State newSubState, bool urgent = false, bool canPropagateUp = false)
        {
            //JmoLogger.Info(this, $"[HSM Debug] CompoundState '{Name}' received transition signal: '{oldSubState?.Name}' → '{newSubState?.Name}' (urgent={urgent}, canPropagateUp={canPropagateUp})");
            //GD.Print($"Attempting to transition FROM '{oldSubState.Name}' TO '{newSubState.Name}'. Current State '{PrimarySubState.Name}'");
            if (!newSubState.IsValid())
            {
                JmoLogger.Error(this, $"Attempted to transition from '{oldSubState.Name}' to a null or invalid state.");
                return;
            }

            // Transition Guard: If not urgent, check if the old state allows exiting.
            // Primarily used for exported StateTransitions in the inspector that don't have all the contextual informtion about the running state.
            if (!urgent && !PrimarySubState.CanExit(newSubState))
            {
                JmoLogger.Debug(this, $"Transition Guard prevents '{PrimarySubState.Name}' from transitioning to '{newSubState}'.");
                return;
            }

            if (canPropagateUp && !FiniteSubStates.ContainsKey(newSubState))
            {
                //JmoLogger.Info(this, $"Couldn't find '{newSubState.Name}' as a child of {Name}. Going up the hierarchy.");
                EmitSignal(SignalName.TransitionState, this, newSubState, urgent, true);
                return;
            }

            if (PrimarySubState != oldSubState)
            {
                // This can happen if a transition signal arrives late, after another transition has already occurred.
                JmoLogger.Warning(this, $"Received transition from '{oldSubState.Name}', but current state is '{PrimarySubState.Name}'. Ignoring stale transition.");
                return;
            }

            PrimarySubState.Exit();
            FiniteSubStates[PrimarySubState] = false;

            PrimarySubState = newSubState;
            FiniteSubStates[PrimarySubState] = true;
            PrimarySubState.Enter();

            EmitSignal(SignalName.TransitionedSubState, oldSubState, newSubState);
            _debugComponent?.OnTransitionedState(oldSubState, newSubState);

            JmoLogger.Debug(this, $"Completed transition FROM '{oldSubState.Name}' TO '{newSubState.Name}'. Current State '{PrimarySubState.Name}'");
        }



        public override string[] _GetConfigurationWarnings()
        {
            var warnings = new List<string>();

            if (InitialSubState == null)
            {
                warnings.Add("InitialSubState is not assigned. This CompoundState will fail on entry.");
            }
            else if (InitialSubState.GetParent() != this)
            {
                warnings.Add("InitialSubState must be a direct child of this CompoundState.");
            }

            // Important: Concatenate warnings from the base class to include its checks.
            return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
        }
    }
