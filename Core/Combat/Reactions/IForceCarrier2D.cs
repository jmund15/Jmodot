namespace Jmodot.Core.Combat.Reactions;

using Godot;

/// <summary>
/// Capability interface for any <see cref="CombatResult"/> that carries kinetic energy.
/// Subscribers (KnockbackComponent2D, KnockbackComponentRigidBody2D, future destructibles)
/// filter incoming results via <c>result is IForceCarrier2D carrier</c> rather than
/// type-switching over concrete CombatResult subtypes — keeps consumers Open/Closed
/// against new force-carrying result types (ExplosionResult, RecoilResult, …).
/// </summary>
public interface IForceCarrier2D
{
    /// <summary>Unit vector defining the impulse direction.</summary>
    Vector2 Direction { get; }
    /// <summary>Impulse magnitude in N·s applied along <see cref="Direction"/>.</summary>
    float Force { get; }
}
