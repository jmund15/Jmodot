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
    /// Modifies the agent's 'SelfInterruptible' property on the blackboard when this task is active.
    /// </summary>
    [Export] protected InterruptibleChange SelfInterruptible = InterruptibleChange.NoChange;

    [ExportGroup("Movement Quirks")]
    /// <summary>
    /// Movement quirks registered on the entity's quirk processor while this action is active.
    /// Registration is refcounted, so a quirk shared with a State or another action survives until
    /// every holder releases it.
    /// </summary>
    [Export] protected GColl.Array<MovementQuirk3D> ActionQuirks { get; private set; } = new();

    private MovementQuirkProcessor3D? _quirkProcessor;

    /// <summary>
    /// Enter() skips OnEnter() when the task's condition check fails, but Exit() runs OnExit()
    /// regardless. Without this latch the unpaired unregister would decrement a refcount this
    /// action never incremented, dropping another owner's live registration.
    /// </summary>
    private bool _quirksRegistered;

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);

        if (ActionQuirks.Count == 0) { return; }

        if (!bb.TryGet<MovementQuirkProcessor3D>(BBDataSig.MovementQuirkProcessor, out var quirkProcessor)
            || quirkProcessor == null)
        {
            throw new NodeConfigurationException(
                "ActionQuirks are assigned but the agent has no MovementQuirkProcessor3D.", this);
        }
        _quirkProcessor = quirkProcessor;
    }

    protected override void OnEnter()
    {
        base.OnEnter();
        switch (this.SelfInterruptible)
        {
            case InterruptibleChange.True:
                this.BB.Set(BBDataSig.SelfInteruptible, true);
                break;
            case InterruptibleChange.False:
                this.BB.Set(BBDataSig.SelfInteruptible, false);
                break;
        }

        foreach (var quirk in ActionQuirks)
        {
            _quirkProcessor?.RegisterQuirk(quirk);
        }
        _quirksRegistered = true;
    }

    protected override void OnExit()
    {
        if (_quirksRegistered)
        {
            foreach (var quirk in ActionQuirks)
            {
                _quirkProcessor?.UnregisterQuirk(quirk);
            }
            _quirksRegistered = false;
        }

        base.OnExit();
    }

    #region Test Helpers
#if TOOLS
    internal void AddActionQuirk(MovementQuirk3D quirk) => ActionQuirks.Add(quirk);
#endif
    #endregion

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
