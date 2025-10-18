namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using Affinities;
using BB;
using Jmodot.AI.Navigation;
using Perception;
using Shared;

/// <summary>
/// The primary coordinator node for an AI entity. It acts as an Orchestrator, owning all
/// core AI components and managing the main perceive-decide-act loop. It provides a clean,
/// high-level API for controlling the agent and is responsible for monitoring the agent's
/// overall state (e.g., detecting if it's stuck).
/// </summary>
[Tool]
[GlobalClass]
public partial class AIAgent3D : Node3D
{
    [ExportGroup("Core Components")]
    [Export] private Blackboard _blackboard = null!;
    [Export] private AIPerceptionManager3D _perceptionManager3D = null!;
    [Export] private AISteeringProcessor3D _steeringProcessor3D = null!;
    [Export] private AINavigator3D _navigator = null!;
    [Export] private AIAffinitiesComponent _affinities = null!;

    /// <summary>
    /// The final output of the AI's decision-making process for this frame.
    /// This normalized vector represents the direction the AI wants to move.
    /// It is read by the character controller to be fed into the MovementProcessor.
    /// </summary>
    public Vector3 DesiredSteeringDirection { get; private set; } = Vector3.Zero;
    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (_blackboard == null) {warnings.Add("Component reference '_blackboard' is not set.");}
        if (_perceptionManager3D == null) {warnings.Add("Component reference '_perceptionManager' is not set.");}
        if (_steeringProcessor3D == null) {warnings.Add("Component reference '_steeringProcessor' is not set.");}
        if (_navigator == null) {warnings.Add("Component reference '_navigator' is not set.");}
        if (_affinities == null) {warnings.Add("Component reference '_affinities' is not set.");}
        if (GetParentOrNull<CharacterBody3D>() == null)
        {
            warnings.Add("AIAgent should be a child of a CharacterBody3D to control movement.");
        }
        return warnings.ToArray();
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }
        // --- Validate, Initialize, and Register all components ---

        // --- Configuration Validation ---
        // A robust agent validates its own dependencies at runtime.
        if (!ValidateCoreComponents())
        {
            SetPhysicsProcess(false);
            return;
        }

        // --- Dependency Initialization ---
        // The agent is responsible for ensuring its components are ready.
        _steeringProcessor3D.Initialize();
        RegisterComponentsOnBlackboard();

        _navigator.TargetReached += OnTargetReached;
        _navigator.VelocityComputed += OnNavigatorVelocityComputed;
    }

    public override void _PhysicsProcess(double delta)
    {
        // High-level logic (e.g., a Behavior Tree) is assumed to have run, updating the blackboard.
        //_blackboard.Get<Vector3>(BBDataSig.TargetPosition, out var highLevelTarget);
        // =======================================================
        // PHASE 1: STRATEGIC DECISION (The "Big Brain")
        // =======================================================
        // Tick the high-level brain to update the overall strategy.
        // This might result in a call like _navigator.NavigateTo(...) if the
        // BT decides to chase a new target.
        //_behaviorTree.Tick(this, _blackboard);

        var targetDirection = this._navigator.GetIdealDirection();

        // --- 1. ASSEMBLE the context for this frame's decision. ---
        // This creates an immutable snapshot of the world state for consistent calculations.
        var context = new SteeringDecisionContext3D(
            _perceptionManager3D,
            _navigator.GetOwner<Node3D>().GlobalPosition, // Get position from navigator's owner
            -_navigator.GetOwner<Node3D>().GlobalBasis.Z,
            _navigator.GetOwner<CharacterBody3D>().Velocity, // Get current velocity from the body
            _navigator.GetIdealDirection(),
            _navigator.TargetPosition
        );

        // --- 2. DECIDE: The steering processor calculates the best direction. ---
        DesiredSteeringDirection = this._steeringProcessor3D.CalculateSteering(context, this._blackboard);

        // --- 3. ACT: The navigator executes the movement. ---
        // TODO: SEND TO THE MOVEMENT PROCESSOR TO GET THE DESIRED VELOCITY, THEN send to navigator for final 'safe' calc
        // This will probably be done in the higher level node (character node) OR whoever has direct access to the movement controller.
        // THis is due to the agent having no way to instantiate or know what the movement processor should be, it's out of scope.
        _navigator.UpdateVelocity(DesiredSteeringDirection);
    }

    /// <summary>
    /// The final step in the movement loop (Act). Receives the safe velocity from the
    /// NavigationAgent3D and applies it to the CharacterBody3D.
    /// </summary>
    private void OnNavigatorVelocityComputed(Vector3 safeVelocity)
    {
    }

    /// <summary>
    /// Called when the navigator signals that the final destination has been reached.
    /// This can be used to trigger events on the Blackboard for the high-level brain.
    /// </summary>
    private void OnTargetReached()
    {
        // Example: Let the Behavior Tree know that the movement is complete.
        _blackboard.Set("MovementResult", "Success");
    }

    #region GRANULAR_CONTROLS & SETUP
        /// <summary>
        /// Enables or disables the entire AI agent, including all its subsystems.
        /// </summary>
        public void SetAgentEnabled(bool isEnabled)
        {
            SetPhysicsProcess(isEnabled);
            SetNavigationEnabled(isEnabled);
            SetPerceptionEnabled(isEnabled);
        }

        /// <summary>
        /// Enables or disables only the navigation and movement-related processing.
        /// </summary>
        public void SetNavigationEnabled(bool isEnabled)
        {
            if (_navigator == null) { return; }
            _navigator.ProcessMode = isEnabled ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;

            if (!isEnabled)
            {
                _navigator.ClearPath();
            }
        }

        /// <summary>
        /// Enables or disables only the perception system.
        /// </summary>
        public void SetPerceptionEnabled(bool isEnabled)
        {
            if (_perceptionManager3D == null) { return; }
            _perceptionManager3D.ProcessMode = isEnabled ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        }

        private bool ValidateCoreComponents()
        {
            if (_blackboard == null || _perceptionManager3D == null || _steeringProcessor3D == null || _navigator == null || _affinities == null)
            {
                JmoLogger.Error(this, "One or more core components are not assigned. Agent cannot function.");
                return false;
            }
            return true;
        }

        private void RegisterComponentsOnBlackboard()
        {
            _blackboard.Set(BBDataSig.PerceptionComp, _perceptionManager3D);
            _blackboard.Set(BBDataSig.Affinities, _affinities);
            _blackboard.Set(BBDataSig.AINavComp, _navigator);
        }
        #endregion
}
