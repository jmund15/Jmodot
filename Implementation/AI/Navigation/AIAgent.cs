namespace Jmodot.Implementation.AI.Navigation;

using System.Collections.Generic;
using Affinities;
using BB;
using Jmodot.AI.Navigation;
using Perception;
using Shared;

/// <summary>
///     The primary coordinator node for an AI entity. It serves as the root of the AI's scene
///     tree, owning all core components and orchestrating the main perceive-decide-act loop
///     every physics frame. It is responsible for initializing dependencies and assembling the
///     DecisionContext for the steering system.
/// </summary>
[Tool]
[GlobalClass]
public partial class AIAgent : Node3D
{
    [Export] private AIAffinitiesComponent _affinities;

    [ExportGroup("Core Components")] [Export]
    private Blackboard _blackboard;

    [Export] private AINavigator3D _navigator;

    [Export] private AIPerceptionManager _perceptionManager;
    [Export] private AISteeringProcessor _steeringProcessor;

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (this._blackboard == null) warnings.Add("Component reference '_blackboard' is not set.");
        if (this._perceptionManager == null) warnings.Add("Component reference '_perceptionManager' is not set.");
        if (this._steeringProcessor == null) warnings.Add("Component reference '_steeringProcessor' is not set.");
        if (this._navigator == null) warnings.Add("Component reference '_navigator' is not set.");
        if (this._affinities == null) warnings.Add("Component reference '_affinities' is not set.");
        return warnings.ToArray();
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;

        // --- Configuration Validation ---
        // A robust agent validates its own dependencies at runtime.
        if (this._blackboard == null || this._perceptionManager == null || this._steeringProcessor == null ||
            this._navigator == null || this._affinities == null)
        {
            JmoLogger.Error(this,
                "One or more core components are not assigned. The agent cannot function and will be disabled.",
                this.GetOwnerOrNull<Node>());
            this.SetPhysicsProcess(false);
            return;
        }

        // --- Dependency Initialization ---
        // The agent is responsible for ensuring its components are ready.
        this._steeringProcessor.Initialize();

        // --- Blackboard Registration ---
        // Ensure the blackboard has references to the core components for other systems (like BTs) to use.
        this._blackboard.SetVar(BBDataSig.Agent, this);
        this._blackboard.SetVar(BBDataSig.PerceptionComp, this._perceptionManager);
        this._blackboard.SetVar(BBDataSig.Affinities, this._affinities);
        this._blackboard.SetVar(BBDataSig.AINavComp, this._navigator); // Assuming AINavComp is the navigator now
    }

    public override void _PhysicsProcess(double delta)
    {
        // High-level logic (e.g., a Behavior Tree) is assumed to have run, updating the blackboard.
        //_blackboard.GetPrimVar<Vector3>(BBDataSig.TargetPosition, out var highLevelTarget);

        var targetDirection = this._navigator.GetIdealDirection();

        // --- 1. ASSEMBLE the context for this frame's decision. ---
        // This creates an immutable snapshot of the world state for consistent calculations.
        var context = new DecisionContext(this._perceptionManager, this.GlobalPosition,
            // TODO: replace with sprite facing direction if applicable (for eyesight dir simulation)
            -this.GlobalBasis.Z, // Standard forward vector in Godot
            this._navigator.Velocity,
            targetDirection, this._navigator.TargetPosition
        );

        // --- 2. DECIDE: The steering processor calculates the best direction. ---
        var desiredDirection = this._steeringProcessor.CalculateSteering(context, this._blackboard);

        // --- 3. ACT: The navigator executes the movement. ---
        this._navigator.SetMovementDirection(desiredDirection);
    }

    // TODO: do some things here that, when disabled, sets variables to default. i.e. navigation would set curr desired direciton to Vector3.Zero, etc.
    public void EnableAgentFull(bool enable)
    {
        this.SetPhysicsProcess(enable);
        this._navigator.ProcessMode = enable ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        this._perceptionManager.ProcessMode = enable ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
    }

    public void EnableAgentNavigation(bool enable)
    {
        this._navigator.ProcessMode = enable ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
    }

    public void EnableAgentPerception(bool enable)
    {
        this._perceptionManager.ProcessMode = enable ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
    }
}
