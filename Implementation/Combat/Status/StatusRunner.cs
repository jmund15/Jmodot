 using Godot;
using System;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

using AI.BB;

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
    public GameplayTag[] Tags { get; set; } = Array.Empty<GameplayTag>();

    protected HitContext Context { get; private set; }
    protected ICombatant Target { get; private set; }

    public event Action<StatusRunner> OnStatusFinished = delegate { };
    public event Action<ICombatEffect, bool> EffectCompleted;

    public virtual void Initialize(HitContext context, ICombatant target, GameplayTag[] tags)
    {
        Context = context;
        Target = target;
        Tags = tags ?? Array.Empty<GameplayTag>();
    }

    /// <summary>
    /// ICombatEffect Implementation.
    /// Adds the runner to the target's StatusEffectComponent and starts it.
    /// </summary>
    public void Apply(ICombatant target, HitContext context)
    {
        if (target.Blackboard.TryGet(BBDataSig.StatusEffects, out StatusEffectComponent statusComp))
        {
            // Initialize before adding to ensure data is ready
            Initialize(context, target, Tags);
            statusComp.AddStatus(this);
        }
        else
        {
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
    public PackedScene PersistentVisuals { get; set; }

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
