namespace Jmodot.Implementation.Physics;

using Godot;
using Shared;

/// <summary>
/// Pure static utility for computing the closest point on a collision shape's surface
/// to an external query point. Used by steering considerations to get accurate
/// distance-to-surface for wide or offset objects instead of origin-to-origin distance.
///
/// Supports BoxShape3D, SphereShape3D, CapsuleShape3D, CylinderShape3D.
/// Unknown shapes fall back to returning the shape center (origin distance).
/// </summary>
public static class ShapeProximityCalculator
{
    /// <summary>
    /// Computes the closest point on a shape's surface to the query point.
    /// The shape is positioned at <paramref name="shapeWorldCenter"/> and oriented
    /// by <paramref name="shapeWorldBasis"/>.
    /// </summary>
    /// <param name="queryPoint">The external point to measure from (e.g., agent position).</param>
    /// <param name="shapeWorldCenter">World-space center of the shape (body.GlobalTransform * collisionShape.Position).</param>
    /// <param name="shapeWorldBasis">World-space orientation of the shape (body.GlobalTransform.Basis).</param>
    /// <param name="shape">The Shape3D resource defining the shape geometry.</param>
    /// <returns>The closest point on the shape's surface in world space.</returns>
    public static Vector3 GetClosestSurfacePoint(
        Vector3 queryPoint, Vector3 shapeWorldCenter, Basis shapeWorldBasis, Shape3D shape)
    {
        return shape switch
        {
            BoxShape3D box => ClosestPointOnBox(queryPoint, shapeWorldCenter, shapeWorldBasis, box),
            SphereShape3D sphere => ClosestPointOnSphere(queryPoint, shapeWorldCenter, sphere),
            CapsuleShape3D capsule => ClosestPointOnCapsule(queryPoint, shapeWorldCenter, shapeWorldBasis, capsule),
            CylinderShape3D cylinder => ClosestPointOnCylinder(queryPoint, shapeWorldCenter, shapeWorldBasis, cylinder),
            _ => shapeWorldCenter
        };
    }

    /// <summary>
    /// Convenience method: iterates a body's CollisionShape3D children and returns
    /// the closest surface point across all shapes. Falls back to body.GlobalPosition
    /// if no collision shapes are found.
    /// </summary>
    public static Vector3 GetClosestSurfacePointOnBody(Vector3 queryPoint, Node3D body)
    {
        float bestDistSq = float.MaxValue;
        Vector3 bestPoint = body.GlobalPosition;
        bool foundShape = false;

        var children = body.GetChildren();

        foreach (var child in children)
        {
            if (child is not CollisionShape3D { Shape: not null } cs) { continue; }
            var center = body.GlobalTransform * cs.Position;
            TryUpdateClosest(queryPoint, center, body.GlobalTransform.Basis, cs.Shape,
                ref bestDistSq, ref bestPoint, ref foundShape);
        }

        if (foundShape) { return bestPoint; }

        // Fallback: check grandchildren (e.g., CollisionShape3D inside HurtboxComponent3D)
        foreach (var child in children)
        {
            if (child is not Node3D childNode) { continue; }
            foreach (var grandchild in childNode.GetChildren())
            {
                if (grandchild is not CollisionShape3D { Shape: not null } cs) { continue; }
                var center = body.GlobalTransform * (childNode.Transform * cs.Position);
                TryUpdateClosest(queryPoint, center, body.GlobalTransform.Basis, cs.Shape,
                    ref bestDistSq, ref bestPoint, ref foundShape);
            }
        }

        return bestPoint;
    }

    private static void TryUpdateClosest(
        Vector3 queryPoint, Vector3 shapeWorldCenter, Basis shapeWorldBasis, Shape3D shape,
        ref float bestDistSq, ref Vector3 bestPoint, ref bool foundShape)
    {
        var point = GetClosestSurfacePoint(queryPoint, shapeWorldCenter, shapeWorldBasis, shape);
        float distSq = queryPoint.DistanceSquaredTo(point);

        if (distSq < bestDistSq)
        {
            bestDistSq = distSq;
            bestPoint = point;
            foundShape = true;
        }
    }

    #region Shape-Specific Implementations

    private static Vector3 ClosestPointOnBox(
        Vector3 queryPoint, Vector3 center, Basis basis, BoxShape3D box)
    {
        var halfSize = box.Size / 2f;

        // Transform query point to box-local space
        var localQuery = basis.Inverse() * (queryPoint - center);

        // Clamp to box extents
        var clamped = new Vector3(
            Mathf.Clamp(localQuery.X, -halfSize.X, halfSize.X),
            Mathf.Clamp(localQuery.Y, -halfSize.Y, halfSize.Y),
            Mathf.Clamp(localQuery.Z, -halfSize.Z, halfSize.Z));

        // If query is inside the box (clamped == localQuery), push to nearest face
        if (clamped.IsEqualApprox(localQuery))
        {
            // Find axis with smallest penetration depth and push to that face
            float dx = halfSize.X - Mathf.Abs(localQuery.X);
            float dy = halfSize.Y - Mathf.Abs(localQuery.Y);
            float dz = halfSize.Z - Mathf.Abs(localQuery.Z);

            if (dx <= dy && dx <= dz)
            {
                clamped.X = localQuery.X > 0 ? halfSize.X : -halfSize.X;
            }
            else if (dy <= dx && dy <= dz)
            {
                clamped.Y = localQuery.Y > 0 ? halfSize.Y : -halfSize.Y;
            }
            else
            {
                clamped.Z = localQuery.Z > 0 ? halfSize.Z : -halfSize.Z;
            }
        }

        // Transform back to world space
        return center + basis * clamped;
    }

    private static Vector3 ClosestPointOnSphere(
        Vector3 queryPoint, Vector3 center, SphereShape3D sphere)
    {
        var toQuery = queryPoint - center;
        if (toQuery.LengthSquared() < 0.0001f)
        {
            // Query at center — return deterministic point (top of sphere)
            return center + Vector3.Up * sphere.Radius;
        }

        return center + toQuery.Normalized() * sphere.Radius;
    }

    private static Vector3 ClosestPointOnCapsule(
        Vector3 queryPoint, Vector3 center, Basis basis, CapsuleShape3D capsule)
    {
        // Capsule: line segment along local Y axis + radius
        // Half-height of the line segment (between hemisphere centers) = height/2 - radius
        float halfSegment = capsule.Height / 2f - capsule.Radius;

        // Transform to local space
        var localQuery = basis.Inverse() * (queryPoint - center);

        // Clamp Y to the line segment [-halfSegment, +halfSegment]
        float clampedY = Mathf.Clamp(localQuery.Y, -halfSegment, halfSegment);

        // Closest point on the segment
        var segmentPoint = new Vector3(0, clampedY, 0);

        // Extend from segment point toward query by radius (sphere around segment point)
        var toQuery = localQuery - segmentPoint;
        if (toQuery.LengthSquared() < 0.0001f)
        {
            // Query on the axis — return point at radius in +X direction
            return center + basis * (segmentPoint + new Vector3(capsule.Radius, 0, 0));
        }

        var surfaceLocal = segmentPoint + toQuery.Normalized() * capsule.Radius;
        return center + basis * surfaceLocal;
    }

    private static Vector3 ClosestPointOnCylinder(
        Vector3 queryPoint, Vector3 center, Basis basis, CylinderShape3D cylinder)
    {
        float halfHeight = cylinder.Height / 2f;

        // Transform to local space
        var localQuery = basis.Inverse() * (queryPoint - center);

        // Clamp Y to cylinder height
        float clampedY = Mathf.Clamp(localQuery.Y, -halfHeight, halfHeight);

        // XZ distance from axis
        var xzOffset = new Vector2(localQuery.X, localQuery.Z);
        float xzDist = xzOffset.Length();

        Vector3 surfaceLocal;
        if (xzDist < 0.0001f)
        {
            // Query on the axis — return point at radius in +X
            surfaceLocal = new Vector3(cylinder.Radius, clampedY, 0);
        }
        else if (xzDist > cylinder.Radius)
        {
            // Outside cylinder — clamp to wall
            var xzNorm = xzOffset / xzDist;
            surfaceLocal = new Vector3(xzNorm.X * cylinder.Radius, clampedY, xzNorm.Y * cylinder.Radius);
        }
        else
        {
            // Inside cylinder — push to nearest surface (wall vs caps)
            float wallDist = cylinder.Radius - xzDist;
            float capDist = halfHeight - Mathf.Abs(localQuery.Y);

            if (wallDist <= capDist)
            {
                // Push to wall
                var xzNorm = xzOffset / xzDist;
                surfaceLocal = new Vector3(xzNorm.X * cylinder.Radius, clampedY, xzNorm.Y * cylinder.Radius);
            }
            else
            {
                // Push to nearest cap
                surfaceLocal = new Vector3(localQuery.X, localQuery.Y > 0 ? halfHeight : -halfHeight, localQuery.Z);
            }
        }

        return center + basis * surfaceLocal;
    }

    #endregion
}
