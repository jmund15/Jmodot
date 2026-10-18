namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using Core.AI;
using Core.AI.Navigation;
using Core.AI.Navigation.Considerations;
using Core.Shared;
using Jmodot.AI.Navigation;
using Navigation.Considerations;
using Shared;
using GColl = Godot.Collections;

/// <summary>
/// A BehaviorAction that registers steering considerations on enter and unregisters
/// them on exit. Returns Running indefinitely — duration control is delegated to
/// BTConditions (TimeLimit, RandomTimeLimit, etc.).
///
/// This enables composable steering behaviors: attach wander considerations for wander,
/// flee considerations for flee — same action class, different data.
/// </summary>
[GlobalClass, Tool]
public partial class SteeringBehaviorAction : BehaviorAction
{
    /// <summary>
    /// Style and environment terms that bend how this step moves. They never decide where the step
    /// goes or when it ends: a MoveAction's goal owns that. An empty list is legal.
    /// </summary>
    [Export] private GColl.Array<BaseAIConsideration3D> _considerations = new();

    /// <summary>The authored style terms, read-only. Their runtime registrations belong to the processor.</summary>
    protected IReadOnlyList<BaseAIConsideration3D> Considerations => _considerations;

    [ExportGroup("Navigation Path Override")]
    [Export] private NavigationPath3DConsideration? _navPathOverride;

    /// <summary>Optional per-action synthesis strategy claimed on enter (owned-slot, owner = task Name)
    /// and released on exit. A conflicting concurrent claim is rejected+warned by the processor.</summary>
    [ExportGroup("Synthesis Override")]
    [Export] private SteeringSynthesisStrategy3D? _synthesisOverride;

    public override string[] _GetConfigurationWarnings()
    {
        // The editor has no Init'ed agent, so the action itself stands in for the entity walk.
        var agentForWarnings = (Node?)Agent ?? this;
        return base._GetConfigurationWarnings()
            .Concat(ConfigWarnings.ResourceSlotWarnings("Considerations", _considerations))
            .Concat(ConfigWarnings.AgentSlotWarnings("Considerations", _considerations, agentForWarnings))
            .ToArray();
    }

    private AISteeringProcessor3D? _cachedSteering;

    /// <summary>
    /// Enter() skips OnEnter() when the task's condition check fails, but Exit() runs OnExit()
    /// regardless. Consideration registration is not refcounted, so an unpaired unregister would
    /// drop another owner's live registration of the same consideration .tres.
    /// </summary>
    private bool _considerationsRegistered;

    protected override void OnEnter()
    {
        base.OnEnter();

        if (!TryGetSteering(out var steering))
        {
            JmoLogger.Error(this, "No AISteeringProcessor3D found on agent — cannot register considerations.");
            Status = TaskStatus.Failure;
            return;
        }

        foreach (var consideration in _considerations)
        {
            steering.RegisterConsideration(consideration);
        }

        if (_navPathOverride != null)
        {
            steering.OverrideNavPathConsideration(_navPathOverride);
        }

        if (_synthesisOverride != null)
        {
            steering.TrySetSynthesisOverride(Name, _synthesisOverride);
        }

        _considerationsRegistered = true;
    }

    protected sealed override void OnProcessPhysics(float delta)
        => OnProcessSteeringPhysics(delta);

    /// <summary>Runs action-specific steering and lifecycle work.</summary>
    protected virtual void OnProcessSteeringPhysics(float delta) { }

    protected override void OnExit()
    {
        base.OnExit();

        if (!_considerationsRegistered) { return; }
        _considerationsRegistered = false;

        if (!TryGetSteering(out var steering)) { return; }

        foreach (var consideration in _considerations)
        {
            steering.UnregisterConsideration(consideration);
        }

        if (_navPathOverride != null)
        {
            steering.ClearNavPathOverride();
        }

        if (_synthesisOverride != null)
        {
            steering.ClearSynthesisOverride(Name);
        }
    }

    protected bool TryGetSteering(out AISteeringProcessor3D steering)
    {
        if (_cachedSteering != null)
        {
            steering = _cachedSteering;
            return true;
        }

        if (Agent.TryGetFirstChildOfType(out AISteeringProcessor3D? found, includeSubChildren: true))
        {
            _cachedSteering = found;
            steering = found;
            return true;
        }

        steering = null!;
        return false;
    }

    #region Test Helpers
#if TOOLS
    internal void AddConsideration(BaseAIConsideration3D consideration) => _considerations.Add(consideration);
    internal IReadOnlyList<BaseAIConsideration3D> AuthoredConsiderationsForTest => _considerations;
    internal void SetNavPathOverride(NavigationPath3DConsideration? navPath) => _navPathOverride = navPath;
    internal void SetSynthesisOverride(SteeringSynthesisStrategy3D? strategy) => _synthesisOverride = strategy;
#endif
    #endregion
}
