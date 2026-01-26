namespace Jmodot.Core.Combat;

using System.Collections.Generic;

/// <summary>
/// Interface for entities that provide explicit combat-level exceptions.
/// Hitboxes check this to skip hits against excepted targets.
/// </summary>
/// <remarks>
/// This solves the Area3D/PhysicsBody3D collision exception gap:
/// - PhysicsBody3D.AddCollisionExceptionWith() only affects physics (MoveAndSlide)
/// - Area3D overlap detection (used by Hitbox/Hurtbox) is completely independent
///
/// By implementing this interface, entities can specify targets that should be
/// skipped during combat hit detection, even when physics exceptions aren't applicable.
///
/// Common use case: Sibling spells from the same spawn burst should not damage each other,
/// but may not be PhysicsBody3D (or physics exceptions may be temporary).
/// </remarks>
public interface ICombatExceptionProvider
{
    /// <summary>
    /// Set of instance IDs that should be excluded from combat hit detection.
    /// Returns null if no combat exceptions are active.
    /// </summary>
    /// <remarks>
    /// Use Node.GetInstanceId() to get the ulong identifier for targets.
    /// The hitbox checks if the target's instance ID is in this set before processing hits.
    /// </remarks>
    HashSet<ulong>? CombatExceptionIds { get; }
}
