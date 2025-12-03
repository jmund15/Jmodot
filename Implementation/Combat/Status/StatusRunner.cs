 using Godot;
using System;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;
using AI.BB;
using Shared;

/// <summary>
/// Base class for all runtime status logic.
/// These are Nodes that exist as children of the StatusEffectComponent.
/// They now implement ICombatEffect directly to allow Factories to return them.
/// </summary>
public abstract partial class StatusRunner : Node, ICombatEffect
{
    /// <summary>
    /// Tags associated with this status (e.g., "Stun", "Poison", "Buff").
    /// Used by the StatusEffectComponent to track active states.
    /// </summary>
    public IEnumerable<GameplayTag> Tags { get; set; } = [];

    protected HitContext Context { get; private set; }
    protected ICombatant Target { get; private set; }

    public event Action<StatusRunner> OnStatusFinished = delegate { };
    public event Action<ICombatEffect, bool> EffectCompleted;

    /// <summary>
    /// ICombatEffect Implementation.
    /// Adds the runner to the target's StatusEffectComponent and starts it.
    /// </summary>
    public void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            // Initialize before adding to ensure data is ready
            Context = context;
            Target = target;
            statusComp!.AddStatus(this);
        }
        else
        {
            JmoLogger.Warning(this, $"Can't apply status as '{target.GetUnderlyingNode().Name}' has no StatusEffectComponent!");
            // Failed to apply (no component), so we just finish immediately
            EffectCompleted?.Invoke(this, false);
            QueueFree();
        }
    }

    /// <summary>
    /// ICombatEffect Implementation.
    /// Cancels the effect.
    /// </summary>
    public void Cancel()
    {
        Stop();
    }

    /// <summary>
    /// Optional visual scene to spawn and hold for the duration of the status.
    /// </summary>
    public PackedScene? PersistentVisuals { get; set; }

    private Node _visualInstance;

    /// <summary>
    /// Called when the status is added to the component.
    /// </summary>
    public virtual void Start()
    {
        if (PersistentVisuals != null)
        {
            _visualInstance = PersistentVisuals.Instantiate();
            AddChild(_visualInstance);
        }
    }

    /// <summary>
    /// Called when the status is removed or finished.
    /// </summary>
    public virtual void Stop()
    {
        if (_visualInstance != null && IsInstanceValid(_visualInstance))
        {
            _visualInstance.QueueFree();
            _visualInstance = null;
        }

        OnStatusFinished?.Invoke(this);
        EffectCompleted?.Invoke(this, true);
        QueueFree();
    }
}
