namespace Jmodot.Implementation.Physics.Collision;

/// <summary>
/// Result of applying a collision physics strategy.
/// Distinguishes between "physics applied" (consume count),
/// "physics skipped" (persist without side effects), and "failed" (destroy).
/// </summary>
public enum PhysicsApplyResult
{
    /// <summary>Physics were applied (velocity changed). Consume count, apply stat modifiers, apply self-damage.</summary>
    Applied,

    /// <summary>Persist without physics (e.g., closingSpeed ≤ 0). Skip count, stat modifiers, and self-damage.</summary>
    Skipped,

    /// <summary>Physics failed (e.g., host not a CharacterController). Destroy the entity.</summary>
    Failed
}
