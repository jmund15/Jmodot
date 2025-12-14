 using Godot;
using System;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;
using AI.BB;
using Core.Combat.Reactions;
using Shared;

/// <summary>
/// Base class for all runtime status logic.
/// These are Nodes that exist as children of the StatusEffectComponent.
/// They now implement ICombatEffect directly to allow Factories to return them.
/// </summary>
public abstract partial class StatusRunner : Node
{
    // Fired when this runner is done (for any reason).
    // The StatusComponent listens to this to clean up.
    // wasDispelled: true = manually stopped/dispelled, false = completed naturally
    public event Action<StatusRunner, bool> OnStatusFinished = delegate { };

    /// <summary>
    /// Tags associated with this status (e.g., "Stun", "Poison", "Buff").
    /// Used by the StatusEffectComponent to track active states.
    /// </summary>
    public IEnumerable<GameplayTag> Tags { get; set; } = [];
    /// <summary>
    /// Optional visual scene to spawn and hold for the duration of the status.
    /// </summary>
    public PackedScene? PersistentVisuals { get; set; }
    private Node? _visualInstance;

    protected HitContext Context { get; private set; }
    protected ICombatant Target { get; private set; }


    /// <summary>
    /// ICombatEffect Implementation.
    /// Cancels the effect.
    /// </summary>
    public void Cancel()
    {
        Stop(true);
    }
    public virtual void Start(ICombatant target, HitContext context)
    {
        Target = target;
        Context = context;

        if (PersistentVisuals != null)
        {
            _visualInstance = PersistentVisuals.Instantiate();
            AddChild(_visualInstance);
        }
        // Subclasses implement specific logic (Timers, Visuals)
    }

    /// <summary>
    /// Called when the status is removed or finished.
    /// </summary>
    /// <param name="wasDispelled"></param>
    public virtual void Stop(bool wasDispelled = false)
    {
        if (_visualInstance != null && IsInstanceValid(_visualInstance))
        {
            _visualInstance.QueueFree();
            _visualInstance = null;
        }

        OnStatusFinished?.Invoke(this, wasDispelled);
        QueueFree();
    }
}
