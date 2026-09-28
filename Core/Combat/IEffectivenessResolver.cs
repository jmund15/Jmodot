namespace Jmodot.Core.Combat;

using Godot;
using Jmodot.Core.Identification;

/// <summary>
/// Effectiveness-resolution seam read through
/// <see cref="Jmodot.Implementation.Combat.IncomingMagnitude.Resolve"/>. Decouples the framework's
/// hit and tick paths from project-specific damage-effectiveness systems.
///
/// <para>
/// Implementations resolve the scalar by which a hit's incoming damage is scaled for the
/// <c>(attacker identity, defender identity)</c> pair. The value is applied as a pure operand
/// on the payload channel (<c>ICombatant.ProcessPayload</c>'s <c>incomingMagnitudeScale</c>) —
/// never stored on <c>HitContext</c>, never a status re-application double-scale. A value of
/// <c>0.0f</c> suppresses the damage-bearing outcomes for that hit (absolute immunity); a value
/// of <c>1.0f</c> leaves the hit untouched (the neutral intrinsic).
/// </para>
///
/// <para>
/// Wired via <c>CombatFactoryDefaults.EffectivenessResolver</c> at project autoload time
/// (static-seam pattern) and consulted once per hit and once per damage-over-time tick, so an
/// implementation must be cheap and side-effect free; null is graceful — the scale is <c>1.0f</c>.
/// </para>
/// </summary>
public interface IEffectivenessResolver
{
    /// <summary>
    /// Resolves the magnitude scale for the pair. Pure query — never applies damage, never has
    /// side effects.
    /// </summary>
    /// <param name="attacker">The attacking identity (categories carried by the attacker node).</param>
    /// <param name="defender">The defending identity (categories carried by the defender node).</param>
    /// <param name="attackerNode">The attacker node, or null when the caller has none; null means no
    /// weight provider, the intrinsic case.</param>
    /// <param name="defenderNode">The defender node, or null; defender-side weights are consumed by
    /// project-side <c>ICategoryWeightProvider</c> implementers.</param>
    /// <returns>The magnitude scale: <c>1.0f</c> neutral, <c>0.0f</c> absolute immunity.</returns>
    float Resolve(Identity attacker, Identity defender, Node? attackerNode, Node? defenderNode);
}
