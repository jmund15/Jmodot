namespace Jmodot.Core.Combat;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.Identification;

/// <summary>
/// Reaction-resolution seam consumed by <c>HurtboxComponent3D.ProcessHit</c>. Decouples
/// the framework's hurtbox from project-specific reaction outcome systems (PushinPotions's
/// <c>OutcomeStrategy</c> hierarchy and <c>Reaction</c> resources).
///
/// <para>
/// Implementations resolve all matching reactions for a hit's
/// <c>(attacker identity, defender identity, defender active status tags)</c> triple
/// and call <c>OutcomeStrategy.Apply(...)</c> on each one's <c>DefenderOutcome</c> in
/// priority order. Outcomes are diverse (damage, status cleanse, VFX spawn, transform,
/// future custom strategies); the resolver doesn't inspect outcome internals — it just
/// delegates to <c>Apply()</c>. The single decision the resolver makes about the forwarded
/// payload: whether to <b>strip base damage</b>, controlled by
/// <see cref="ReactionCompositionMode.Exclusive"/> on any matched reaction.
/// </para>
///
/// <para>
/// Wired via <c>CombatFactoryDefaults.ReactionResolver</c> at project autoload time
/// (static-seam pattern). Hurtboxes pull the resolver from there each hit; null is fine
/// (hurtbox falls through to its existing pre-A2 path).
/// </para>
/// </summary>
public interface IReactionResolver
{
    /// <summary>
    /// Consult reactions for the hit and apply outcomes. Returns the payload the hurtbox
    /// should forward to <c>ProcessPayload</c>. The returned payload equals
    /// <paramref name="originalPayload"/> when no reactions matched; otherwise it may be a
    /// wrapper with the base <c>DamageEffect</c> stripped (when an Exclusive reaction matched).
    /// </summary>
    /// <param name="attackerIdentity">The attacking spell's <c>Identity</c>, resolved via
    /// <c>IIdentifiable.GetIdentity()</c> on the attacker node hierarchy.</param>
    /// <param name="defenderIdentity">The defender's <c>Identity</c>, resolved similarly
    /// from the combatant's owner.</param>
    /// <param name="defenderActiveTags">All <c>CombatTag</c>s currently active on the defender
    /// (typically aggregated from the defender's <c>StatusEffectComponent</c>). Empty list is
    /// fine; reactions that gate on status will simply not match.</param>
    /// <param name="attackerNode">The attacker node (typically <c>payload.Attacker</c> — used
    /// by the implementation to walk up the tree and find an <c>IReactionTarget</c>).</param>
    /// <param name="defenderNode">The defender node (typically <c>combatant.OwnerNode</c> — used
    /// by the implementation to walk up the tree and find an <c>IReactionTarget</c>).</param>
    /// <param name="originalPayload">The unmodified attack payload. Implementation may return
    /// it as-is or wrap it (e.g., to strip damage); MUST never return null.</param>
    /// <param name="hitContext">The hit context built by the hurtbox.</param>
    /// <returns>The payload to forward; never null.</returns>
    IAttackPayload ConsultAndApply(
        Identity attackerIdentity,
        Identity defenderIdentity,
        IReadOnlyList<CombatTag> defenderActiveTags,
        Node attackerNode,
        Node defenderNode,
        IAttackPayload originalPayload,
        HitContext hitContext);
}
