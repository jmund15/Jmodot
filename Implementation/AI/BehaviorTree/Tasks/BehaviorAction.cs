namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using BB;
using Core.AI.BB;
using Core.AI.BehaviorTree;
using Core.Actors;
using Core.Movement.Quirks;
using Core.Stats;
using Movement.Quirks;
using Movement.Strategies;
using Shared;
using Shared.GodotExceptions;
using Stats;
using GColl = Godot.Collections;

/// <summary>
/// Represents a leaf node in the Behavior Tree. Actions are where the actual work
/// (e.g., moving, attacking, playing an animation) is performed.
/// </summary>
[GlobalClass, Tool]
public abstract partial class BehaviorAction : BehaviorTask
{
    /// <summary>
    /// Movement quirks registered on the entity's quirk processor while this action is active.
    /// Registration is refcounted, so a quirk shared with a State or another action survives until
    /// every holder releases it.
    /// </summary>
    [ExportGroup("Movement Quirks")]
    [Export] protected GColl.Array<MovementQuirk3D> MovementQuirks { get; private set; } = new();

    /// <summary>
    /// The state's rhythm does not apply while this step runs. All drive gates pause their clocks;
    /// impulse quirks still run. Use for an escape or pursuit that must not stall.
    /// </summary>
    [Export] public bool SuspendGait { get; private set; }

    private MovementQuirkRegistration _quirkRegistration;

    /// <summary>
    /// Optional movement strategy held for the whole action. Actions that phase-latch their own
    /// movement override must leave this unset; authoring both scopes would make the claims fight.
    /// </summary>
    [ExportGroup("Movement Override")]
    [Export] public BaseMovementStrategy3D? MovementStrategyOverride { get; private set; }

    private MovementOverrideLatch _movementOverrideLatch;
    private StatContextRegistration _statContextRegistration;

    /// <summary>
    /// Optional stat context applied while this action runs: its modifiers install on enter and
    /// drop on exit. The provider refcounts a context, so one shared with a State or another
    /// action survives until every holder releases it.
    /// </summary>
    [ExportGroup("Stat Context")]
    [Export] public StatContext? ActiveStatContext { get; private set; }

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        _quirkRegistration.Resolve(bb, MovementQuirks, this, SuspendGait);
        _statContextRegistration.Resolve(bb, ActiveStatContext);
        bb.TryGet<IMovementProcessor3D>(BBDataSig.MovementProcessor, out var movement);
        if (this.MovementStrategyOverride != null && movement == null)
        {
            throw new NodeConfigurationException(
                $"BehaviorAction '{this.Name}' has a MovementStrategyOverride but BB.MovementProcessor is not registered.", this);
        }
    }

    protected override void OnEnter()
    {
        base.OnEnter();
        this.BB.TryGet<IMovementProcessor3D>(BBDataSig.MovementProcessor, out var movement);
        this._movementOverrideLatch.Apply(movement, this.MovementStrategyOverride);
        this._quirkRegistration.Register();
        this._statContextRegistration.Apply();
    }

    protected override void OnExit()
    {
        this._movementOverrideLatch.Restore();
        this._statContextRegistration.Remove();
        this._quirkRegistration.Release();

        base.OnExit();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (this.GetChildren().OfType<BehaviorTask>().Any())
        {
            warnings.Add("BehaviorAction must be a leaf node and cannot have BehaviorTask children.");
        }
        if (this.MovementStrategyOverride != null && MovementOverrideNesting.DescribeConflict(this) is { } conflict)
        {
            warnings.Add(conflict);
        }

        if (this.SuspendGait)
        {
            var ownGait = this.MovementQuirks.OfType<CadenceQuirk3D>().FirstOrDefault();
            if (ownGait != null)
            {
                var name = string.IsNullOrEmpty(ownGait.ResourceName) ? "unnamed Cadence gait" : ownGait.ResourceName;
                warnings.Add(
                    $"SuspendGait and this step's Cadence gait '{name}' conflict. SuspendGait wins; " +
                    "remove the gait from this action or turn SuspendGait off.");
            }
        }

        warnings.AddRange(ConfigWarnings.QuirkSlotWarnings(this.MovementQuirks));

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #region Test Helpers
#if TOOLS
    internal void SetMovementStrategyOverride(BaseMovementStrategy3D? strategy) => this.MovementStrategyOverride = strategy;
    internal void SetActiveStatContext(StatContext? context) => this.ActiveStatContext = context;
    internal void SetSuspendGaitForTest(bool suspendGait) => this.SuspendGait = suspendGait;
    internal void AddMovementQuirkForTest(MovementQuirk3D quirk) => this.MovementQuirks.Add(quirk);
    internal void ClearMovementQuirksForTest() => this.MovementQuirks.Clear();

    /// <summary>Copies the authored slot of <paramref name="replaced"/> (conditions, monitoring, gait opt-out, quirks) onto this task.</summary>
    internal void AdoptSlotFromForTest(BehaviorAction replaced)
    {
        foreach (var condition in replaced.Conditions) { this.Conditions.Add(condition); }
        this.MonitorConditions = replaced.MonitorConditions;
        this.SuspendGait = replaced.SuspendGait;
        foreach (var quirk in replaced.MovementQuirks) { this.MovementQuirks.Add(quirk); }
    }
#endif
    #endregion
}
