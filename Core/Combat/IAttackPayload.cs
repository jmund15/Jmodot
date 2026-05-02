using System.Collections.Generic;
using Jmodot.Core.Stats;

namespace Jmodot.Core.Combat;

public interface IAttackPayload
{
    /// <summary>
    /// The Entity responsible for the attack.
    /// </summary>
    Node Attacker { get; }

    /// <summary>
    /// The specific object (Weapon/Projectile) representing the attack.
    /// </summary>
    Node Source { get; }

    /// <summary>
    /// The list of logic instructions (Damage, Stun, etc.) to apply.
    /// </summary>
    IReadOnlyList<ICombatEffect> Effects { get; }

    /// <summary>
    /// The attacker's stat provider (e.g., the spell's <c>StatController</c>), exposed so
    /// downstream consumers — capacity providers, reaction resolvers, payload interceptors —
    /// can read the attacker's runtime stats without re-resolving the attacker. May be
    /// <c>null</c> for attackers that have no stat provider; consumers MUST null-check.
    /// </summary>
    IStatProvider? Stats { get; }

    // TODO: add a float or DamageEffect get; property for Base Damage. autoset for convenience. this would be zero if the payload has no direct base damage (status-only),
    //  and otherwise be the payload's main DamageEffect damage number.
}

