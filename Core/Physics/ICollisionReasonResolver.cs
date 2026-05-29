namespace Jmodot.Core.Physics;

using Godot;

/// <summary>
/// Resolves the coarse <see cref="CollisionReason"/> for a collision against a
/// given collider and contact normal. Wired once at startup via
/// <see cref="CollisionDefaults.ReasonResolver"/>; consuming projects may supply
/// their own implementation to override the shipped default.
/// </summary>
public interface ICollisionReasonResolver
{
    CollisionReason Resolve(Node collider, Vector3 normal);
}
