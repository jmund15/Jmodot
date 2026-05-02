namespace Jmodot.Core.Combat;

/// <summary>
/// Controls whether a matched reaction's outcomes <b>replace</b> the spell's base combat
/// effect or <b>layer on top of</b> it. Authored on each <c>Reaction</c> resource.
///
/// <para>
/// <b>Resolution rule.</b> When the defender's hurtbox is hit, the resolver finds all
/// matching reactions and calls <c>OutcomeStrategy.Apply(...)</c> on each one's
/// <c>DefenderOutcome</c> in priority order. Outcomes are diverse (damage, status cleanse,
/// VFX spawn, transform, future custom strategies) and each does its own work via Apply.
/// The mode controls a single resolver decision: <b>does the spell's base
/// <c>DamageEffect</c> get stripped from the forwarded payload?</b>
/// <code>
/// any_exclusive = false
/// for each matched reaction (priority order):
///     reaction.DefenderOutcome.Apply(context, defender)
///     if reaction.CompositionMode == Exclusive: any_exclusive = true
/// if any_exclusive: strip base DamageEffect from forwarded payload
/// </code>
/// </para>
///
/// <para>
/// <b>Why this is the right axis.</b> The OutcomeStrategy hierarchy is the diverse-effect
/// axis (damage, spawn, cleanse, etc. — each strategy plugs in cleanly). The mode is
/// orthogonal: it tells the resolver whether the spell's own damage event still fires.
/// "Exclusive" reactions are ones where the reaction IS the new combat effect (shatter,
/// oil+fire→explosion, instant-kill, absorption); "Additive" reactions are extras that
/// don't displace the spell's own damage (burning-floor amp, status-cleanse-on-hit).
/// </para>
///
/// <para>
/// <b>Default is <see cref="Additive"/></b> — preserves base-damage flow-through, the
/// historical behavior. Authors opt into Exclusive only when the reaction's outcomes
/// fully account for the hit's damage event.
/// </para>
/// </summary>
public enum ReactionCompositionMode
{
    /// <summary>
    /// Outcomes layer ON TOP of the spell's base damage. The spell's <c>DamageEffect</c>
    /// flows through to <c>ProcessPayload</c> normally; the reaction's outcomes are
    /// additional effects (extra damage events, status applies, VFX, etc.). Use for
    /// reactions that are "extras" — e.g., burning-floor amp, wet-enemy slow-harder,
    /// status-cleanse-on-hit, secondary spawns.
    /// </summary>
    Additive = 0,

    /// <summary>
    /// Outcomes REPLACE the spell's base combat effect. The spell's <c>DamageEffect</c>
    /// is stripped from the forwarded payload (so <c>ProcessPayload</c> won't deliver
    /// base damage); the reaction's outcomes are the complete new combat effect. Use for
    /// reactions where the reaction IS the damage event — shatter on frozen, oil+fire→
    /// explosion (spawns explosion entity, suppresses spell's normal damage), instant-kill,
    /// absorption.
    /// </summary>
    Exclusive = 1,
}
