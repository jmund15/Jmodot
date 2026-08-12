namespace Jmodot.Implementation.Physics;

using Godot;
using Shared;

/// <summary>
/// Places a physics body so its own lowest collider point rests flush on the surface beneath it.
/// A spawn point then only has to mark WHERE a body goes; the body's collider decides at what height,
/// so anchor Y stops being a load-bearing convention that a too-low value embeds and a too-high one
/// turns into a visible settle drop.
/// </summary>
public static class BodyGroundSnapper
{
    private const float ProbeUpOffset = 0.25f;
    private const float ProbeDistance = 8f;

    /// <summary>
    /// Returns <paramref name="desired"/> with its origin.Y shifted so the body's lowest collider point
    /// sits on the first surface found straight down from it; XZ and basis are preserved. The probe uses
    /// the body's OWN <see cref="CollisionObject3D.CollisionMask"/> — whatever the body would stand on is
    /// what it grounds against — and excludes the body itself.
    /// Returns false, echoing <paramref name="desired"/> unchanged, when the body carries no measurable
    /// collider or nothing lies under the probe.
    /// <para>
    /// <paramref name="body"/> must already be inside the tree: the space state comes from its world,
    /// and an out-of-tree body has none.
    /// </para>
    /// </summary>
    public static bool TryGround(PhysicsBody3D body, Transform3D desired, out Transform3D grounded)
    {
        grounded = desired;
        if (!body.IsInsideTree()) { return false; }
        if (!GroundOffsetCalculator.TryCalculateLowestLocalY(body, out float lowestLocalY)) { return false; }

        Vector3 origin = desired.Origin;
        var query = PhysicsRayQueryParameters3D.Create(
            origin + (Vector3.Up * ProbeUpOffset),
            origin + (Vector3.Down * ProbeDistance),
            body.CollisionMask);
        query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };

        Godot.Collections.Dictionary hit = body.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) { return false; }

        float supportY = ((Vector3)hit["position"]).Y;
        grounded = new Transform3D(desired.Basis, new Vector3(origin.X, supportY - lowestLocalY, origin.Z));
        return true;
    }

    /// <summary>
    /// Grounds <paramref name="body"/> against the transform it already carries, writing the result back.
    /// A first-attempt miss is retried once on the next physics frame before anything is reported: a body
    /// placed on the frame its surroundings are built probes a physics world that has not published those
    /// static bodies yet, and only a step makes them hittable. A body still ungroundable after that retry
    /// keeps its transform and is reported loudly — the signal that a spawn point is over void or a
    /// collider is unreadable.
    /// <para>
    /// The immediate return value therefore answers "was it grounded NOW", not "will it be grounded" — a
    /// false with a retry pending is the common case for build-frame spawns.
    /// </para>
    /// </summary>
    public static bool GroundInPlace(PhysicsBody3D body, GodotObject logContext)
    {
        if (TryGround(body, body.GlobalTransform, out Transform3D grounded))
        {
            body.GlobalTransform = grounded;
            return true;
        }

        if (body.IsInsideTree())
        {
            body.GetTree().Connect(
                SceneTree.SignalName.PhysicsFrame,
                Callable.From(() => RetryGround(body, logContext)),
                (uint)GodotObject.ConnectFlags.OneShot);
            return false;
        }

        WarnUngrounded(body, logContext);
        return false;
    }

    private static void RetryGround(PhysicsBody3D body, GodotObject logContext)
    {
        // A body queued for deletion is still valid and in-tree on the frame the retry lands, so the
        // queued state is the only signal that grounding it would be reporting on a corpse.
        if (!GodotObject.IsInstanceValid(body) || !body.IsInsideTree() || body.IsQueuedForDeletion()) { return; }

        if (TryGround(body, body.GlobalTransform, out Transform3D grounded))
        {
            body.GlobalTransform = grounded;
            return;
        }

        WarnUngrounded(body, logContext);
    }

    private static void WarnUngrounded(PhysicsBody3D body, GodotObject logContext)
    {
        GodotObject context = GodotObject.IsInstanceValid(logContext) ? logContext : body;
        JmoLogger.Warning(context,
            $"[GroundSnap] '{body.Name}' at {body.GlobalPosition} could not be grounded (nothing under the probe, "
            + "or no collider extent to measure); leaving it at its placement height.");
    }
}
