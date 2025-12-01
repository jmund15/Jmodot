using System.Collections.Generic;
using Godot;

namespace Jmodot.Core.Combat;

/// <summary>
/// A concrete, mutable implementation of IAttackPayload for Spells.
/// Allows SpellEffects to dynamically add or remove combat effects (Damage, Status)
/// during the spell's lifecycle.
/// </summary>
public class CombatPayload : IAttackPayload
{
    public Node Attacker { get; }
    public Node Source { get; }

    private readonly List<ICombatEffect> _effects = new();
    public IReadOnlyList<ICombatEffect> Effects => _effects;

    public CombatPayload(Node attacker, Node source)
    {
        Attacker = attacker;
        Source = source;
    }

    public void AddEffect(ICombatEffect effect)
    {
        if (effect != null)
        {
            _effects.Add(effect);
        }
    }

    public void RemoveEffect(ICombatEffect effect)
    {
        _effects.Remove(effect);
    }
    
    public void ClearEffects()
    {
        _effects.Clear();
    }
}
