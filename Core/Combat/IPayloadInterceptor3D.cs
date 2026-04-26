namespace Jmodot.Core.Combat;

using Jmodot.Implementation.Combat;

/// <summary>
/// Pre-hit hook that allows filtering or modifying an attack payload before it reaches a 3D target.
/// Implementers receive the target hurtbox and original payload, and return the payload to use
/// for ProcessHit. The original payload is always preserved for OnHitRegistered, so post-hit
/// observers (e.g., reaction systems) can still extract base damage from the unfiltered payload.
///
/// Contract:
/// - NEVER return null. Return the original payload if no filtering is needed.
/// - Return an empty-effects payload to suppress all combat effects while still allowing
///   ProcessHit validation and OnHitReceived to fire.
/// - Implementers must be safe to invoke from inside HitboxComponent3D.TryHitHurtbox
///   (which may run during a physics callback).
///
/// 2D entities use the parallel <see cref="IPayloadInterceptor2D"/> contract.
/// </summary>
public interface IPayloadInterceptor3D
{
    /// <summary>
    /// Intercept and optionally filter an attack payload before it reaches the target.
    /// </summary>
    /// <param name="target">The hurtbox being hit (use to resolve the defender entity).</param>
    /// <param name="payload">The original attack payload with all effects.</param>
    /// <returns>The payload to use for ProcessHit. Never null.</returns>
    IAttackPayload InterceptPayload(HurtboxComponent3D target, IAttackPayload payload);
}
