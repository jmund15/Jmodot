namespace Jmodot.Implementation.Physics;

using Godot;

/// <summary>
/// Pure static utility for computing the ground offset of collision shapes.
/// Given a shape's type, local position, and scale, returns how far below
/// the parent's origin the shape's lowest point extends.
///
/// Used by <see cref="Jmodot.Core.Physics.IGroundable"/> implementations
/// to auto-calculate ground placement offset.
/// </summary>
public static class GroundOffsetCalculator
{
    /// <summary>
    /// Computes the distance from the parent origin to the bottom of a shape.
    /// Returns 0 if the shape's bottom is at or above the parent origin.
    /// </summary>
    /// <param name="shape">The Shape3D resource (SphereShape3D, CapsuleShape3D, BoxShape3D).</param>
    /// <param name="localPosition">The CollisionShape3D node's local position within its parent.</param>
    /// <param name="nodeScale">The CollisionShape3D node's scale (NOT the parent's scale).</param>
    /// <returns>Positive float representing the distance below parent origin, or 0.</returns>
    public static float CalculateBottomExtent(Shape3D shape, Vector3 localPosition, Vector3 nodeScale)
    {
        float halfExtentY = shape switch
        {
            SphereShape3D sphere => sphere.Radius,
            CapsuleShape3D capsule => capsule.Height / 2f,
            BoxShape3D box => box.Size.Y / 2f,
            _ => 0f
        };

        float scaledExtent = halfExtentY * Mathf.Abs(nodeScale.Y);
        float bottomY = localPosition.Y - scaledExtent;

        // Only return offset if bottom is below parent origin
        return bottomY < 0f ? Mathf.Abs(bottomY) : 0f;
    }

    /// <summary>
    /// Convenience method that reads shape, position, and scale directly from a CollisionShape3D node.
    /// </summary>
    public static float CalculateFromCollisionShape(CollisionShape3D collisionShape)
    {
        if (collisionShape.Shape == null) { return 0f; }

        return CalculateBottomExtent(
            collisionShape.Shape,
            collisionShape.Position,
            collisionShape.Scale);
    }
}
