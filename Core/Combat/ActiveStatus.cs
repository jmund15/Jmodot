using Godot;
using System;

namespace Jmodot.Core.Combat;

/// <summary>
/// Represents a runtime instance of a status effect on an entity.
/// Created by a CombatEffect resource when applied.
/// </summary>
public abstract class ActiveStatus
{
    /// <summary>
    /// The source that applied this status (e.g., Player, Trap).
    /// </summary>
    public Node Source { get; private set; }

    /// <summary>
    /// The entity this status is applied to.
    /// </summary>
    public ICombatant Target { get; private set; }

    /// <summary>
    /// If true, this status has finished and should be removed.
    /// </summary>
    public abstract bool IsFinished { get; }

    public ActiveStatus(Node source, ICombatant target)
    {
        Source = source;
        Target = target;
    }

    /// <summary>
    /// Called when the status is first added to the component.
    /// </summary>
    public virtual void OnApply() { }

    /// <summary>
    /// Called every frame (or tick) by the StatusEffectComponent.
    /// </summary>
    /// <param name="delta">Time since last frame.</param>
    public virtual void OnTick(double delta) { }

    /// <summary>
    /// Called when the status is removed (expired or cleansed).
    /// </summary>
    public virtual void OnRemove() { }
}
