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

        [ExportGroup("Debugging")]
        /// <summary>
        /// If true, a debug overlay will be instantiated to visualize the state machine's activity.
        /// This should typically only be enabled on the root-most CompoundState.
        /// </summary>
        [Export]
        private bool _enableDebugView = false;
        [Export]
        private DebugViewPosition _debugViewPosition = DebugViewPosition.TopRight;

        public State PrimarySubState { get; protected set; }

        public Dictionary<State, bool> FiniteSubStates { get; protected set; } = new();
        public Dictionary<State, bool> ParallelSubStates { get; protected set; } = new();

        private DebugSMComponent _debugComponent;

        [Signal]
        public delegate void EnteredCompoundStateEventHandler();
        [Signal]
        public delegate void ExitedCompoundStateEventHandler();
        [Signal]
        public delegate void TransitionedSubStateEventHandler(State oldState, State newState);

        public override void Init(Node agent, IBlackboard bb)
        {
            base.Init(agent, bb);

            if (_enableDebugView && !Engine.IsEditorHint())
            {
                _debugComponent = new DebugSMComponent();
                AddChild(_debugComponent);
            }

            foreach (var child in GetChildren().OfType<State>())
            {
                child.Init(agent, bb);
                child.TransitionState += TransitionFiniteSubState;
                child.AddParallelState += AddParallelSubState;
                child.RemoveParallelState += RemoveParallelSubState;

                if (child is IParallelState)
                {
                    ParallelSubStates.Add(child, false);
                }
                else
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

            PrimarySubState = InitialSubState;
            FiniteSubStates[PrimarySubState] = true;
            PrimarySubState.Enter(ParallelSubStates);

            EmitSignal(SignalName.EnteredCompoundState);
            _debugComponent?.OnEnteredCompoundState(PrimarySubState);
        }

        protected override void OnExit()
        {
            base.OnExit();
            PrimarySubState?.Exit();
            if(PrimarySubState.IsValid())
            {
                FiniteSubStates[PrimarySubState] = false;
            }

            foreach (var parallelState in ParallelSubStates.Where(ps => ps.Value))
            {
                parallelState.Key.Exit();
            }

            EmitSignal(SignalName.ExitedCompoundState);
            _debugComponent?.OnExitedCompoundState();
        }

        protected override void OnProcessFrame(float delta)
        {
            base.OnProcessFrame(delta);
            PrimarySubState?.ProcessFrame(delta);
            foreach (var parallelState in ParallelSubStates.Where(ps => ps.Value))
            {
                parallelState.Key.ProcessFrame(delta);
            }
        }

        protected override void OnProcessPhysics(float delta)
        {
            base.OnProcessPhysics(delta);
            PrimarySubState?.ProcessPhysics(delta);

            foreach (var parallelState in ParallelSubStates.Where(ps => ps.Value))
            {
                parallelState.Key.ProcessPhysics(delta);
            }
        }

        protected override void OnHandleInput(InputEvent @event)
        {
            base.OnHandleInput(@event);
            PrimarySubState?.HandleInput(@event);
            foreach (var parallelState in ParallelSubStates.Where(ps => ps.Value))
            {
                parallelState.Key.HandleInput(@event);
            }
        }

        public virtual void TransitionFiniteSubState(State oldSubState, State newSubState)
        {
            if (!newSubState.IsValid())
            {
                JmoLogger.Error(this, $"Attempted to transition from '{oldSubState.Name}' to a null or invalid state.");
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
            PrimarySubState.Enter(ParallelSubStates);

            EmitSignal(SignalName.TransitionedSubState, oldSubState, newSubState);
            _debugComponent?.OnTransitionedState(oldSubState, newSubState);
        }

        public virtual void AddParallelSubState(State state)
        {
            if (state is not IParallelState)
            {
                JmoLogger.Error(this, $"Attempted to add '{state.Name}' as a parallel state, but it does not implement IParallelState.");
                return;
            }
            if (ParallelSubStates.ContainsKey(state) && ParallelSubStates[state])
            {
                JmoLogger.Warning(this, $"Cannot add parallel state '{state.Name}' because it is already active.");
                return;
            }

            ParallelSubStates[state] = true;
            state.Enter(ParallelSubStates);
        }

        public virtual void RemoveParallelSubState(State state)
        {
            if (state is not IParallelState)
            {
                JmoLogger.Error(this, $"Attempted to remove '{state.Name}' as a parallel state, but it does not implement IParallelState.");
                return;
            }
            if (!ParallelSubStates.ContainsKey(state) || !ParallelSubStates[state])
            {
                JmoLogger.Warning(this, $"Cannot remove parallel state '{state.Name}' because it is not active.");
                return;
            }

            ParallelSubStates[state] = false;
            state.Exit();
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
