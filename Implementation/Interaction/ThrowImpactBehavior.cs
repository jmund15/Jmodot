namespace Jmodot.Implementation.Interaction;

using Godot;

/// <summary>
/// Modular, designer-assignable behavior invoked when a thrown object impacts something.
/// Plugged into <see cref="ThrowableComponent3D"/> via an <c>[Export]</c>; a null behavior
/// makes the projectile inert on impact. Mirrors the Resource-strategy pattern of
/// <c>InteractionFeedbackStrategy</c> / <c>BaseMovementStrategy3D</c>.
/// </summary>
[GlobalClass, Tool]
public abstract partial class ThrowImpactBehavior : Resource
{
    /// <param name="thrown">The thrown object that impacted.</param>
    /// <param name="hit">The node that was hit, or null if none was resolved.</param>
    /// <param name="velocity">The thrown object's velocity at impact.</param>
    public abstract void OnImpact(Node3D thrown, Node3D? hit, Vector3 velocity);
}
