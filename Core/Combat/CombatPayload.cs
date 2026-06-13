using System.Collections.Generic;
using Godot;
using Jmodot.Core.Stats;

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
    public IStatProvider? Stats { get; }
    public int? AttackSeed { get; }
    public SeedProvenance SeedProvenance { get; }

    private readonly List<ICombatEffect> _effects = new();
    public IReadOnlyList<ICombatEffect> Effects => _effects;

    public CombatPayload(Node attacker, Node source, IStatProvider? stats = null,
        int? attackSeed = null, SeedProvenance provenance = SeedProvenance.Missing)
    {
        Attacker = attacker;
        Source = source;
        Stats = stats;
        AttackSeed = attackSeed;
        SeedProvenance = provenance;
    }

    /// <summary>
    /// Returns a copy carrying the given attack-seed lineage, preserving Attacker/Source/Stats
    /// and the effects list. The single propagation chokepoint — wrapper/filter sites that
    /// rebuild a payload route through here so the lineage token is never dropped.
    /// </summary>
    public CombatPayload With(int? attackSeed, SeedProvenance provenance)
    {
        var copy = new CombatPayload(Attacker, Source, Stats, attackSeed, provenance);
        copy._effects.AddRange(_effects);
        return copy;
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

    /// <summary>
    /// Replaces the effect at the given index with a new one. Used for live mutation
    /// of value-type effects (e.g. DamageEffect is a readonly struct — to "scale" its
    /// damage we rebuild and replace, rather than mutate in place).
    /// No-op if index is out of range or newEffect is null.
    /// </summary>
    public void ReplaceEffectAt(int index, ICombatEffect newEffect)
    {
        if (index < 0 || index >= _effects.Count) { return; }
        if (newEffect == null) { return; }
        _effects[index] = newEffect;
    }

    public void ClearEffects()
    {
        _effects.Clear();
    }
}
