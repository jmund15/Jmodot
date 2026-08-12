namespace Jmodot.Implementation.Physics;

using Godot;
using Shared;

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

    /// <summary>
    /// Lowest point of <paramref name="body"/>'s OWN enabled <see cref="CollisionShape3D"/> children,
    /// in body-local space. SIGNED, unlike <see cref="CalculateBottomExtent"/>: negative when the
    /// collider hangs below the body origin, positive when it sits entirely above it — a caller
    /// grounding a body needs both directions, and clamping the second to zero embeds the body.
    /// Each shape's axis-aligned extents are pushed through its transform relative to the body, so
    /// offsets, rotation and scale are all accounted for.
    /// <para>
    /// Direct children only, because a <see cref="CollisionShape3D"/> belongs to its IMMEDIATE
    /// <see cref="CollisionObject3D"/> parent: sensor Areas parented under a body (pickup radii,
    /// hurtboxes, interactors) are separate collision objects the body never rests on, and measuring
    /// them lifts it into the air by their radius.
    /// </para>
    /// Returns false when no shape of a supported type is found.
    /// </summary>
    public static bool TryCalculateLowestLocalY(CollisionObject3D body, out float lowestLocalY)
    {
        lowestLocalY = 0f;
        bool found = false;
        Transform3D toBody = body.GlobalTransform.AffineInverse();

        foreach (CollisionShape3D collisionShape in body.GetChildrenOfType<CollisionShape3D>(includeSubChildren: false))
        {
            if (collisionShape.Disabled || collisionShape.Shape == null) { continue; }
            if (!TryGetHalfExtents(collisionShape.Shape, out Vector3 half)) { continue; }

            Transform3D relative = toBody * collisionShape.GlobalTransform;
            for (int corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    (corner & 1) == 0 ? -half.X : half.X,
                    (corner & 2) == 0 ? -half.Y : half.Y,
                    (corner & 4) == 0 ? -half.Z : half.Z);
                float y = (relative * local).Y;
                if (!found || y < lowestLocalY)
                {
                    lowestLocalY = y;
                    found = true;
                }
            }
        }

        return found;
    }

    private static bool TryGetHalfExtents(Shape3D shape, out Vector3 halfExtents)
    {
        switch (shape)
        {
            case SphereShape3D sphere:
                halfExtents = new Vector3(sphere.Radius, sphere.Radius, sphere.Radius);
                return true;
            case BoxShape3D box:
                halfExtents = box.Size / 2f;
                return true;
            case CapsuleShape3D capsule:
                halfExtents = new Vector3(capsule.Radius, capsule.Height / 2f, capsule.Radius);
                return true;
            case CylinderShape3D cylinder:
                halfExtents = new Vector3(cylinder.Radius, cylinder.Height / 2f, cylinder.Radius);
                return true;
            default:
                halfExtents = Vector3.Zero;
                return false;
        }
    }
}
