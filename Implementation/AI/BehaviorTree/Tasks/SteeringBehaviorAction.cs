namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using Core.AI;
using Core.AI.Navigation;
using Core.AI.Navigation.Considerations;
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
    [Export] private GColl.Array<BaseAIConsideration3D> _considerations = new();

    [ExportGroup("Navigation Path Override")]
    [Export] private NavigationPath3DConsideration? _navPathOverride;

    /// <summary>Optional move/pause cadence. Null means continuous steering.</summary>
    [ExportGroup("Movement Cadence")]
    [Export] public MovementCadenceResource? Cadence { get; private set; }

    /// <summary>Optional per-action synthesis strategy claimed on enter (owned-slot, owner = task Name)
    /// and released on exit. A conflicting concurrent claim is rejected+warned by the processor.</summary>
    [ExportGroup("Synthesis Override")]
    [Export] private SteeringSynthesisStrategy3D? _synthesisOverride;

    private AISteeringProcessor3D? _cachedSteering;
    private bool _cadencePaused;
    private float _cadenceTimer;
    private bool _cadenceClaimed;

    /// <summary>
    /// Enter() skips OnEnter() when the task's condition check fails, but Exit() runs OnExit()
    /// regardless. Consideration registration is not refcounted, so an unpaired unregister would
    /// drop another owner's live registration of the same consideration .tres.
    /// </summary>
    private bool _considerationsRegistered;

    protected override void OnEnter()
    {
        base.OnEnter();

        _cadencePaused = false;
        _cadenceTimer = Cadence?.MoveSeconds ?? 0f;

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
    {
        var steeringAllowed = TickCadence(delta);
        UpdateCadenceControl(steeringAllowed);
        OnProcessSteeringPhysics(delta);
    }

    /// <summary>Runs action-specific steering and lifecycle work after cadence state updates.</summary>
    protected virtual void OnProcessSteeringPhysics(float delta) { }

    /// <summary>True when the cadence permits a destination or consideration update this tick.</summary>
    protected bool CadenceAllowsSteering => !_cadencePaused;

    private bool TickCadence(float delta)
    {
        if (Cadence == null || !(Cadence.MoveSeconds > 0f))
        {
            _cadencePaused = false;
            return true;
        }

        var moving = MovementCadenceResource.AdvanceCadence(
            _cadencePaused, _cadenceTimer, Cadence.MoveSeconds, Cadence.PauseSeconds, delta,
            out _cadencePaused, out _cadenceTimer);
        return moving;
    }

    private void UpdateCadenceControl(bool steeringAllowed)
    {
        if (steeringAllowed)
        {
            if (_cadenceClaimed && _cachedSteering != null)
            {
                _cachedSteering.ReleaseControl(this.Name);
                _cadenceClaimed = false;
            }
            return;
        }

        if (_cadenceClaimed || !TryGetSteering(out var steering)) { return; }
        if (!steering.TryClaimControl(this.Name, SteeringControlMode.DirectionOverride, Godot.Vector3.Zero)) { return; }
        _cadenceClaimed = true;
    }

    protected override void OnExit()
    {
        if (_cadenceClaimed && _cachedSteering != null)
        {
            _cachedSteering.ReleaseControl(this.Name);
            _cadenceClaimed = false;
        }
        _cadencePaused = false;
        _cadenceTimer = 0f;

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

    private bool TryGetSteering(out AISteeringProcessor3D steering)
    {
        if (_cachedSteering != null)
        {
            steering = _cachedSteering;
            return true;
        }

        if (Agent.TryGetFirstChildOfType(out AISteeringProcessor3D? found))
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
    internal void SetNavPathOverride(NavigationPath3DConsideration? navPath) => _navPathOverride = navPath;
    internal void SetSynthesisOverride(SteeringSynthesisStrategy3D? strategy) => _synthesisOverride = strategy;
#endif
    #endregion
}
