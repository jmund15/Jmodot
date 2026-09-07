namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using Core.AI;
using BB;
using Core.AI.BB;
using Core.AI.BehaviorTree;
using Core.Movement;
using Core.Shared;
using Examples.AI.BehaviorTree.Conditions;
using Jmodot.AI.Navigation;
using Jmodot.Core.Actors;
using Movement.Strategies;
using Shared;
using Shared.GodotExceptions;

/// <summary>
/// A BT leaf that steers the agent along exact, repeatedly re-picked directions. Rigid,
/// self-clocked weaving — a feint, a sidestep shuffle, a strafing jitter.
/// <para>
/// Returns Running indefinitely — duration control is delegated to a wrapping BTCondition
/// (<c>RandomTimeLimit</c>, <c>TimeLimit</c>) per the house convention (<see cref="SteeringBehaviorAction"/>).
/// </para>
/// <para>
/// Dither is a hard locomotion <em>override</em>: it claims the steering processor's
/// <see cref="SteeringControlMode.DirectionOverride"/> slot so leash, separation and danger stop
/// weighing in and the agent turns exactly rather than approximately. That is the requirement.
/// Organic, blended weave is <c>SinusoidalLateralConsideration3D</c> and belongs in the
/// consideration set instead.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class DitherAction : BehaviorAction
{
    /// <summary>Shortest gap between direction re-picks, in seconds. Zero on both bounds never flips.</summary>
    [ExportGroup("Timing")]
    [Export(PropertyHint.Range, "0.0, 5.0, 0.01, or_greater")]
    public float FlipIntervalMin { get; private set; } = 0.15f;

    /// <summary>Longest gap between direction re-picks, in seconds. Re-rolled after every flip.</summary>
    [Export(PropertyHint.Range, "0.0, 5.0, 0.01, or_greater")]
    public float FlipIntervalMax { get; private set; } = 0.3f;

    /// <summary>
    /// The directions this dither may steer along. Required: without it the action would run
    /// without ever resolving. Unset warns in the editor and throws at init.
    /// </summary>
    [ExportGroup("Direction")]
    [Export] public DirectionSet3D? Directions { get; private set; }

    /// <summary>
    /// Which member of <see cref="Directions"/> each flip selects. Required for the same reason
    /// <see cref="Directions"/> is: unset warns in the editor and throws at init.
    /// </summary>
    [Export] public DitherPickStrategy? PickStrategy { get; private set; }

    /// <summary>The direction picked for the current flip, or <see cref="Vector3.Zero"/> before enter.</summary>
    public Vector3 CurrentDirection { get; private set; }

    /// <summary>How many flips have happened since the last enter.</summary>
    public int FlipIndex { get; private set; }

    private bool _steeringClaimed;
    private AISteeringProcessor3D? _steering;
    private IRng _rng = null!;
    private float _sinceFlip;
    private float _flipInterval;

    /// <inheritdoc />
    /// <exception cref="NodeConfigurationException">
    /// <see cref="Directions"/> or <see cref="PickStrategy"/> is unset, or
    /// <see cref="BehaviorAction.MovementStrategyOverride"/> is set while the blackboard carries no movement
    /// processor. Mirrors <see cref="HSM.BTState.OnInit"/>: a required slot fails loud once, never as
    /// a per-use warning.
    /// </exception>
    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);

        if (this.Directions == null)
        {
            throw new NodeConfigurationException(
                $"DitherAction '{this.Name}' requires a Directions set; without one it can only run without ever resolving.", this);
        }

        if (this.PickStrategy == null)
        {
            throw new NodeConfigurationException(
                $"DitherAction '{this.Name}' requires a PickStrategy; without one it can only run without ever resolving.", this);
        }

        if (this.Directions.OrderedDirections.Count == 0)
        {
            throw new NodeConfigurationException(
                $"DitherAction '{this.Name}' has an empty Directions set; every pick returns Zero, which claims " +
                "DirectionOverride toward nowhere and pins the agent with leash and separation suppressed.", this);
        }

        this._rng = JmoRng.UnseededByDesign();
        if (bb.TryGet<int>(BBDataSig.EntitySeed, out var seed))
        {
            this._rng = JmoRng.FromRawStreamName($"Dither:{this.Name}", seed);
        }
        else
        {
            JmoLogger.Warning(this, $"[BT] DitherAction '{this.Name}' found no EntitySeed; its weave is unseeded.");
        }

        if (!bb.TryGet<AISteeringProcessor3D>(BBDataSig.SteeringComp, out this._steering) || this._steering == null)
        {
            throw new NodeConfigurationException(
                $"DitherAction '{this.Name}' steers by claiming the steering processor's DirectionOverride " +
                "slot, but BB.SteeringComp is not registered; without it the action only runs without ever resolving.", this);
        }
    }

    protected override void OnEnter()
    {
        base.OnEnter();

        this._sinceFlip = 0f;
        this.FlipIndex = 0;
        this._steeringClaimed = false;
        this._flipInterval = RandomTimeLimit.RollInRange(this.FlipIntervalMin, this.FlipIntervalMax, this._rng.GetRndFloat());

        this.CommitDirection();
    }

    protected override void OnProcessPhysics(float delta)
    {
        if (this._flipInterval <= 0f) { return; }

        this._sinceFlip += delta;
        if (this._sinceFlip < this._flipInterval) { return; }

        this._sinceFlip -= this._flipInterval;
        this.FlipIndex++;
        this._flipInterval = RandomTimeLimit.RollInRange(this.FlipIntervalMin, this.FlipIntervalMax, this._rng.GetRndFloat());
        this.CommitDirection();
    }

    protected override void OnExit()
    {
        // Owner-checked release: releasing a slot this action never claimed is a warned no-op, so
        // only release what the claim actually took.
        if (this._steeringClaimed)
        {
            this._steering?.ReleaseControl(this.Name);
            this._steeringClaimed = false;
        }
        this.CurrentDirection = Vector3.Zero;

        base.OnExit();
    }

    private void CommitDirection()
    {
        this.CurrentDirection = this.PickStrategy!.Pick(new DitherPickContext
        {
            Directions = this.Directions!,
            Rng = this._rng,
            FlipIndex = this.FlipIndex,
        });

        if (this._steering == null) { return; }

        // The control slot is owner-keyed and REJECTS a conflicting concurrent claim (the processor
        // warns on its own). A rejected dither steers nothing, so it reports Failure: Success would
        // tell a parent Selector the feint happened and consume the fallback branch it never ran.
        this._steeringClaimed = this._steering.TryClaimControl(
            this.Name, SteeringControlMode.DirectionOverride, this.CurrentDirection);
        if (!this._steeringClaimed)
        {
            this.CurrentDirection = Vector3.Zero;
            this.Status = TaskStatus.Failure;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (this.Directions == null)
        {
            warnings.Add("DitherAction requires a Directions set; without one it only runs without ever resolving.");
        }

        if (this.PickStrategy == null)
        {
            warnings.Add("DitherAction requires a PickStrategy; without one it only runs without ever resolving.");
        }

        if (this.Directions != null && this.Directions.OrderedDirections.Count == 0)
        {
            warnings.Add("DitherAction's Directions set is empty; every pick steers toward nowhere.");
        }

        if (this.FlipIntervalMin > this.FlipIntervalMax)
        {
            warnings.Add($"FlipIntervalMin ({this.FlipIntervalMin}) exceeds FlipIntervalMax ({this.FlipIntervalMax}); the flip band is inverted.");
        }

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #region Test Helpers
#if TOOLS
    internal void SetFlipInterval(float min, float max) { this.FlipIntervalMin = min; this.FlipIntervalMax = max; }
    internal void SetDirections(DirectionSet3D? directions) => this.Directions = directions;
    internal void SetPickStrategy(DitherPickStrategy? strategy) => this.PickStrategy = strategy;
#endif
    #endregion
}
