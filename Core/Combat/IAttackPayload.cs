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
    IReadOnlyList<CombatEffect> Effects { get; }
}

