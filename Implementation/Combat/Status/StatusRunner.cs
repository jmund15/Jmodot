 using Godot;
using System;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;
using System.Linq;
using AI.BB;
using Core.Combat.Reactions;
using Core.Visual.Effects;
using Implementation.Visual.Effects;
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
    public IEnumerable<CombatTag> Tags { get; protected set; } = [];
    /// <summary>
    /// Optional visual scene to spawn and hold for the duration of the status.
    /// </summary>
    public PackedScene? PersistentVisuals { get; protected set; }
    
    /// <summary>
    /// Optional visual effect (tint, flash, shader) to apply to the target during the status.
    /// </summary>
    public VisualEffect? StatusVisualEffect { get; set; }
    
    private bool _stopped;
    private Node? _visualInstance;
    protected VisualEffectController? VisualController { get; private set; }

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

            // TODO: add config for if visuals should be parented to the target or the status effect component
            target.OwnerNode.AddChild(_visualInstance);
        }
        
        if (StatusVisualEffect != null)
        {
            VisualController = FindVisualController(target);
            VisualController?.PlayEffect(StatusVisualEffect);
        }
        
        // Subclasses implement specific logic (Timers, Visuals)
    }

    /// <summary>
    /// Called when the status is removed or finished.
    /// </summary>
    /// <param name="wasDispelled"></param>
    public virtual void Stop(bool wasDispelled = false)
    {
        if (_stopped) { return; }
        _stopped = true;

        if (_visualInstance != null && IsInstanceValid(_visualInstance))
        {
            _visualInstance.QueueFree();
            _visualInstance = null;
        }

        if (StatusVisualEffect != null && VisualController != null && IsInstanceValid(VisualController))
        {
            VisualController.StopEffect(StatusVisualEffect);
        }

        OnStatusFinished?.Invoke(this, wasDispelled);
        QueueFree();
    }
    
    private VisualEffectController? FindVisualController(ICombatant target)
    {
        if (target?.OwnerNode == null) { return null; }
        
        // Try to find in children
        var controller = target.OwnerNode.GetChildrenOfType<VisualEffectController>().FirstOrDefault();
        return controller;
    }
}
