namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using BB;
using Core.AI.BB;
using Core.AI.BehaviorTree;
using Core.Actors;
using Core.Movement.Quirks;
using Movement.Quirks;
using Movement.Strategies;
using Shared;
using Shared.GodotExceptions;
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

    private MovementQuirkRegistration _quirkRegistration;

    /// <summary>
    /// Optional movement strategy held for the whole action. Actions that phase-latch their own
    /// movement override must leave this unset; authoring both scopes would make the claims fight.
    /// </summary>
    [ExportGroup("Movement Override")]
    [Export] public BaseMovementStrategy3D? MovementStrategyOverride { get; private set; }

    private MovementOverrideLatch _movementOverrideLatch;

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        _quirkRegistration.Resolve(bb, MovementQuirks, this);
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
    }

    protected override void OnExit()
    {
        this._movementOverrideLatch.Restore();
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

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    #region Test Helpers
#if TOOLS
    internal void SetMovementStrategyOverride(BaseMovementStrategy3D? strategy) => this.MovementStrategyOverride = strategy;
#endif
    #endregion
}
