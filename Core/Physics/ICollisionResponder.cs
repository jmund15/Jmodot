namespace Jmodot.Core.Physics;

using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Physics.Collision;

/// <summary>
/// Host-independent collision-response contract. Implementations resolve a
/// <see cref="BaseCollisionResponse"/> for a <see cref="CollisionContact"/> and apply the
/// configured physics, returning whether the host should persist (true) or be destroyed
/// by the consumer (false).
///
/// The destroy signal is a returned <c>bool</c> the consumer acts on — the responder never
/// destroys the host itself, preserving each consumer's existing pool-return / free path.
/// </summary>
public interface ICollisionResponder
{
    /// <summary>
    /// Resolves the response for the contact (category match → normal fallback → default) and
    /// dispatches it. Returns true to persist, false to destroy.
    /// </summary>
    bool HandleCollision(ICollisionHost host, CollisionContact contact);

    /// <summary>
    /// Dispatches a collision with an explicitly-provided response, bypassing mapping
    /// resolution. Used by external systems that intercept and resolve responses
    /// independently. All guards (count check, velocity thresholds, frame coalescing,
    /// debounce, stat modifiers) still apply.
    /// </summary>
    bool HandleCollisionWithResponse(ICollisionHost host, CollisionContact contact, BaseCollisionResponse response);

    /// <summary>
    /// Configures the host's physical body at initialization time (e.g., pierce layer masks).
    /// </summary>
    void ConfigureBody(ICollisionHost host, HitboxComponent3D? hitbox);
}
