namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System.Collections.Generic;
using System.Linq;
using BB;
using Core.AI.BB;
using Core.AI.BehaviorTree;
using Core.Movement.Quirks;
using Movement.Quirks;
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

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        _quirkRegistration.Resolve(bb, MovementQuirks, this);
    }

    protected override void OnEnter()
    {
        base.OnEnter();
        _quirkRegistration.Register();
    }

    protected override void OnExit()
    {
        _quirkRegistration.Release();

        base.OnExit();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (this.GetChildren().OfType<BehaviorTask>().Any())
        {
            warnings.Add("BehaviorAction must be a leaf node and cannot have BehaviorTask children.");
        }
        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }
}
