namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Identification;
using Jmodot.Core.Physics;

/// <summary>
/// Default <see cref="ICollisionReasonResolver"/> shipped by Jmodot. Classifies
/// at the coarse <see cref="CollisionReason"/> granularity: identifiable bodies
/// and physics bodies are <see cref="CollisionReason.Entity"/>; everything else
/// falls back to a contact-normal heuristic (up-facing → Ground, else Wall).
/// Projects needing finer reasons supply their own resolver via the seam.
/// </summary>
/// <remarks>
/// Two intentional coarseness choices a finer-grained consumer should be aware of:
/// (1) the <see cref="IIdentifiable"/> check inspects the collider root only — it does
/// not walk children, unlike a project resolver that may use TryGetFirstChildOfInterface;
/// (2) any identified body classifies as <see cref="CollisionReason.Entity"/> regardless
/// of contact-normal orientation, so an identified floor never resolves to Ground here.
/// </remarks>
public sealed class DefaultCollisionReasonResolver : ICollisionReasonResolver
{
    public CollisionReason Resolve(Node collider, Vector3 normal)
    {
        if (collider is IIdentifiable identifiable && identifiable.GetIdentity() != null)
        {
            return CollisionReason.Entity;
        }

        if (collider is CharacterBody3D or RigidBody3D)
        {
            return CollisionReason.Entity;
        }

        return normal.Dot(Vector3.Up) > 0.5f
            ? CollisionReason.Ground
            : CollisionReason.Wall;
    }
}
