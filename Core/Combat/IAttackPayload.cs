using System.Collections.Generic;

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

    // TODO: add a float or DamageEffect get; property for Base Damage. autoset for convenience. this would be zero if the payload has no direct base damage (status-only),
    //  and otherwise be the payload's main DamageEffect damage number.
}

