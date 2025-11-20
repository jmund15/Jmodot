namespace Jmodot.Core.Combat;


/// <summary>
/// The payload of an attack, containing the attacker's identity and a list of effects to apply.
/// </summary>
public interface IAttackPayload
{
    Node Attacker { get; }
    System.Collections.Generic.IReadOnlyList<CombatEffect> Effects { get; }
}
