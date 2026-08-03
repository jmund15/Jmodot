namespace Jmodot.Core.Combat;

using System.Collections.Generic;

/// <summary>
/// Supplies ADDITIONAL per-swing effect factories to a hitbox, beyond its authored
/// <c>DefaultEffects</c> list. Dimension-agnostic: this contract carries no spatial types, so the
/// 3D and 2D hitboxes each own their own nullable slot of this ONE interface (runtime-wired, never
/// authored in the Inspector — the same shape as <c>PayloadInterceptor</c>).
/// <para>
/// Returned factories are appended AFTER the authored list under the hitbox's single effect-index
/// cursor, so index assignment stays stable for a fixed sequence. A null return is treated as an
/// empty list; null ELEMENTS consume an index and are skipped, matching the authored list's
/// null-slot semantics.
/// </para>
/// <para>
/// A returned <see cref="IDamageContributingFactory"/> contributes its base damage to the swing's
/// fold target (the first damage-contributing factory in <c>DefaultEffects</c>) and produces NO
/// effect of its own — one damage effect, one crit roll. A returned factory that is not marked but
/// whose <c>Create</c> yields a <c>DamageEffect</c> is rejected loudly and not added: a sibling
/// <c>DamageEffect</c> would corrupt first-only base-damage extraction downstream.
/// </para>
/// <para>
/// That rejection is the FULL extent of the framework's damage policing. Damage delivered through
/// any other shape — a derived effect type, or a wrapper factory (delay/tick/duration-revertible)
/// holding a damage factory — is invisible to the check: it does not fold, it keeps its own crit
/// roll, and no error is emitted. A source that composes trait-supplied factories owns authoring-time
/// validation of that class.
/// </para>
/// </summary>
public interface IEffectFactorySource
{
    /// <summary>
    /// The factories to append to this swing. Queried EXACTLY ONCE per attack assembly — mid-swing
    /// mutation of the source lands on the next swing, and a continuous window reuses the payload
    /// built at the initiating attack.
    /// </summary>
    IReadOnlyList<ICombatEffectFactory> GetFactories();
}
