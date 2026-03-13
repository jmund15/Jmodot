namespace Jmodot.Core.Combat;

using Jmodot.Implementation.Combat;

/// <summary>
/// Pre-hit hook that allows filtering or modifying an attack payload before it reaches the target.
/// Implementers receive the target hurtbox and original payload, and return the payload to use
/// for ProcessHit. The original payload is always preserved for OnHitRegistered.
///
/// Contract:
/// - NEVER return null. Return original payload if no filtering needed.
/// - Return empty CombatPayload (zero effects) to suppress all combat effects while still
///   allowing ProcessHit validation and OnHitReceived to fire.
/// </summary>
public interface IPayloadInterceptor
{
    /// <summary>
    /// Intercept and optionally filter an attack payload before it reaches the target.
    /// </summary>
    /// <param name="target">The hurtbox being hit (use to resolve the defender entity).</param>
    /// <param name="payload">The original attack payload with all effects.</param>
    /// <returns>The payload to use for ProcessHit. Never null.</returns>
    IAttackPayload InterceptPayload(HurtboxComponent3D target, IAttackPayload payload);
}
