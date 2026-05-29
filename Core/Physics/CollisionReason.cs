namespace Jmodot.Core.Physics;

/// <summary>
/// Coarse, framework-agnostic classification of what a body collided with.
/// This is the host-independent reason granularity Jmodot bodies reason about;
/// consuming projects that need finer reasons (e.g. PP's StringName spell-reason
/// vocabulary) resolve those in their own layer, orthogonal to this seam.
/// </summary>
public enum CollisionReason
{
    Ground,
    Wall,
    Entity,
    Other,
}
