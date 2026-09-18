namespace Jmodot.Implementation.Physics;

using Godot;

/// <summary>
/// Places a physics body so its own lowest collider point rests flush on the surface beneath it.
/// A spawn point then only has to mark WHERE a body goes; the body's collider decides at what height,
/// so anchor Y stops being a load-bearing convention that a too-low value embeds and a too-high one
/// turns into a visible settle drop.
/// </summary>
/// <remarks>
/// Character bodies are never valid support. Static, animatable, rigid and node-less server-RID
/// colliders remain valid support; adding another standable body type is an authoring decision.
/// </remarks>
public static class BodyGroundSnapper
{
    private const float ProbeUpOffset = 0.25f;
    private const float ProbeDistance = 8f;

    /// <summary>
    /// Returns <paramref name="desired"/> with its origin.Y shifted so the body's lowest collider point
    /// sits on the first surface found straight down from it; XZ and basis are preserved. The probe uses
    /// the body's OWN <see cref="CollisionObject3D.CollisionMask"/> — whatever the body would stand on is
    /// what it grounds against — and excludes the body itself. The probe starts just above the body's
    /// HIGHEST collider point, so a marker authored at the walk surface (collider hanging below it, inside
    /// the floor) still grounds; a body buried deeper than that is an authoring error and is reported.
    /// Returns false, echoing <paramref name="desired"/> unchanged, when the body carries no measurable
    /// collider or nothing lies under the probe.
    /// <para>
    /// <paramref name="body"/> must already be inside the tree: the space state comes from its world,
    /// and an out-of-tree body has none.
    /// </para>
    /// </summary>
    /// <summary>Character bodies the probe will look through before giving up; a stack deeper than this is not a spawn layout.</summary>
    private const int MaxBodiesProbedThrough = 8;

    public static bool TryGround(PhysicsBody3D body, Transform3D desired, out Transform3D grounded)
    {
        grounded = desired;
        if (!body.IsInsideTree()) { return false; }
        if (!GroundOffsetCalculator.TryCalculateLocalYExtent(body, out float lowestLocalY, out float highestLocalY)) { return false; }

        Vector3 origin = desired.Origin;
        // Start above the collider's top, not the origin: a marker authored at the walk surface leaves a
        // collider that hangs below it inside the floor, and a ray that starts inside a shape sees nothing.
        float probeTop = Mathf.Max(highestLocalY, 0f) + ProbeUpOffset;
        var query = PhysicsRayQueryParameters3D.Create(
            origin + (Vector3.Up * probeTop),
            origin + (Vector3.Down * ProbeDistance),
            body.CollisionMask);
        var exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
        query.Exclude = exclude;
        PhysicsDirectSpaceState3D space = body.GetWorld3D().DirectSpaceState;

        // A CharacterBody3D is never support: a layout that wraps placements stands this body on a
        // sibling, and the probe must look through it to the floor beneath, not report no ground.
        Godot.Collections.Dictionary hit = space.IntersectRay(query);
        for (int passes = 0; hit.Count > 0 && hit["collider"].AsGodotObject() is CharacterBody3D blocker; passes++)
        {
            if (passes >= MaxBodiesProbedThrough) { return false; }
            exclude.Add(blocker.GetRid());
            query.Exclude = exclude;
            hit = space.IntersectRay(query);
        }
        if (hit.Count == 0) { return false; }

        float supportY = ((Vector3)hit["position"]).Y;
        grounded = new Transform3D(desired.Basis, new Vector3(origin.X, supportY - lowestLocalY, origin.Z));
        return true;
    }
}
