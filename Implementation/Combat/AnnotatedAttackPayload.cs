namespace Jmodot.Implementation.Combat;

using System.Collections.Generic;
using Core.Combat;

// A simple wrapper class to hold an annotated payload.
public class AnnotatedAttackPayload : IAttackPayload
{
    public Node Attacker { get; }
    public System.Collections.Generic.IReadOnlyList<CombatEffect> Effects { get; }
    public AnnotatedAttackPayload(Node attacker, List<CombatEffect> effects)
    {
        Attacker = attacker;
        Effects = effects;
    }
}
