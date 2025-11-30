using Godot;
using System;
using Jmodot.Core.Combat;

namespace Jmodot.Implementation.Combat.Status;

/// <summary>
/// Base class for all runtime status logic.
/// These are Nodes that exist as children of the StatusEffectComponent.
/// </summary>
public abstract partial class StatusRunner : Node
{
    /// <summary>
    /// Tags associated with this status (e.g., "Stun", "Poison", "Buff").
    /// Used by the StatusEffectComponent to track active states.
    /// </summary>
    public string[] Tags { get; private set; } = Array.Empty<string>();

    protected HitContext Context { get; private set; }
    protected ICombatant Target { get; private set; }

    public event Action<StatusRunner> OnStatusFinished = delegate { };

    public virtual void Initialize(HitContext context, ICombatant target, string[] tags)
    {
        Context = context;
        Target = target;
        Tags = tags ?? Array.Empty<string>();
    }

    /// <summary>
    /// Called when the status is added to the component.
    /// </summary>
    public virtual void Start() { }

    /// <summary>
    /// Called when the status is removed or finished.
    /// </summary>
    public virtual void Stop() 
    {
        OnStatusFinished?.Invoke(this);
        QueueFree();
    }
}
