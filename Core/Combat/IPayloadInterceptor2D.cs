namespace Jmodot.Core.Combat;

using Jmodot.Implementation.Combat;

/// <summary>
/// Pre-hit hook that allows filtering or modifying an attack payload before it reaches a 2D target.
/// 2D twin of <see cref="IPayloadInterceptor3D"/>; same semantics with a 2D hurtbox parameter.
///
/// Contract:
/// - NEVER return null. Return the original payload if no filtering is needed.
/// - Return an empty-effects payload to suppress all combat effects while still allowing
///   ProcessHit validation and OnHitReceived to fire.
/// - Implementers must be safe to invoke from inside HitboxComponent2D.TryHitHurtbox
///   (which may run during a physics callback).
/// </summary>
public interface IPayloadInterceptor2D
{
    /// <summary>
    /// Intercept and optionally filter an attack payload before it reaches the target.
    /// </summary>
    /// <param name="target">The 2D hurtbox being hit (use to resolve the defender entity).</param>
    /// <param name="payload">The original attack payload with all effects.</param>
    /// <returns>The payload to use for ProcessHit. Never null.</returns>
    IAttackPayload InterceptPayload(HurtboxComponent2D target, IAttackPayload payload);
}
