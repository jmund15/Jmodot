namespace Jmodot.Implementation.AI.HSM;

using System.Collections.Generic;
using System.Linq;
using BehaviorTree;
using Core.AI;
using Core.AI.BB;
using Shared;
using Shared.GodotExceptions;

/// <summary>
    /// A specialized state that acts as a bridge between the State Machine and a Behavior Tree.
    /// When this state is entered, it activates a child BehaviorTree node. It transitions to
    /// other states based on the success or failure of the Behavior Tree's execution.
    /// </summary>
    [GlobalClass, Tool]
    public partial class BTState : State
    {
        /// <summary>
        /// The state to transition to when the Behavior Tree completes with a SUCCESS status.
        /// </summary>
        [Export(PropertyHint.NodeType, "State")]
        protected State OnTreeSuccessState;

        /// <summary>
        /// The state to transition to when the Behavior Tree completes with a FAILURE status.
        /// </summary>
        [Export(PropertyHint.NodeType, "State")]
        protected State OnTreeFailureState;

        private BehaviorTree _tree;

        public override void Init(Node agent, IBlackboard bb)
        {
            base.Init(agent, bb);
            if (!this.TryGetFirstChildOfType<BehaviorTree>(out _tree, false))
            {
                throw new NodeConfigurationException($"BTState '{Name}' requires a child node of type BehaviorTree.", this);
            }

            _tree.Init(agent, bb);
            _tree.TreeFinishedLoop += OnTreeFinishLoop;
        }

        protected override void OnEnter()
        {
            base.OnEnter();
            _tree.Enter();
        }

        protected override void OnExit()
        {
            base.OnExit();
            _tree.Exit();
        }

        private void OnTreeFinishLoop(TaskStatus treeStatus)
        {
            JmoLogger.Info(this, $"BehaviorTree finished loop with status: {treeStatus}");
            switch (treeStatus)
            {
                case TaskStatus.FAILURE:
                    if (!OnTreeFailureState.IsValid())
                    {
                        JmoLogger.Warning(this, "Tree failed, but OnTreeFailureState is not set. The BT will restart in this state.");
                        return;
                    }
                    EmitSignal(SignalName.TransitionState, this, OnTreeFailureState);
                    break;
                case TaskStatus.SUCCESS:
                    if (!OnTreeSuccessState.IsValid())
                    {
                        JmoLogger.Warning(this, "Tree succeeded, but OnTreeSuccessState is not set. The BT will restart in this state.");
                        return;
                    }
                    EmitSignal(SignalName.TransitionState, this, OnTreeSuccessState);
                    break;
                case TaskStatus.RUNNING or TaskStatus.FRESH:
                    JmoLogger.Error(this, "TreeFinishedLoop signal was emitted with a non-terminal status. This indicates a logic error in the BehaviorTree.");
                    break;
            }
        }

        public override string[] _GetConfigurationWarnings()
        {
            var warnings = new List<string>();

            if (!this.TryGetFirstChildOfType<BehaviorTree>(out _, false))
            {
                warnings.Add("BTState must contain a child of type BehaviorTree.");
            }

            if (OnTreeSuccessState == null)
            {
                warnings.Add("OnTreeSuccessState is not assigned. The state machine will remain in this state upon BT success.");
            }
            else if (Engine.IsEditorHint() && OnTreeSuccessState.GetParent() != GetParent())
            {
                // A common configuration error is pointing to a state in a different FSM.
                // A valid target state should almost always be a sibling.
                warnings.Add("OnTreeSuccessState is not a sibling of this BTState. Ensure it belongs to the same CompoundState.");
            }

            if (OnTreeFailureState == null)
            {
                warnings.Add("OnTreeFailureState is not assigned. The state machine will remain in this state upon BT failure.");
            }
            else if (Engine.IsEditorHint() && OnTreeFailureState.GetParent() != GetParent())
            {
                warnings.Add("OnTreeFailureState is not a sibling of this BTState. Ensure it belongs to the same CompoundState.");
            }

            // Concatenate warnings from the base State class.
            return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
        }
    }
