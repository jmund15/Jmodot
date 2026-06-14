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
    /// Returns a copy that RESTAMPS the lineage token (AttackSeed/SeedProvenance), preserving
    /// Attacker/Source/Stats and the FULL effects list unchanged. Use when re-seeding an existing
    /// payload without altering its effects (reserved for the planned reaction-chain reseed; no
    /// production caller yet). Orthogonal to <see cref="RebuildWithEffects"/>: this swaps the seed
    /// keeping effects; that swaps the effects keeping the seed.
    /// </summary>
    public CombatPayload With(int? attackSeed, SeedProvenance provenance)
    {
        var copy = new CombatPayload(Attacker, Source, Stats, attackSeed, provenance);
        copy._effects.AddRange(_effects);
        return copy;
    }

    /// <summary>
    /// Rebuilds a payload from <paramref name="original"/> with a DIFFERENT effect set, preserving
    /// Attacker/Source/Stats AND the lineage token (AttackSeed/SeedProvenance). The single
    /// seed-preserving path for effect-selective filters — routing rebuilds through here means a
    /// filter can't silently drop the lineage token (the historical Stats/seed-drop bug). Null
    /// effects in <paramref name="effects"/> are skipped (matches <see cref="AddEffect"/>).
    /// </summary>
    public static CombatPayload RebuildWithEffects(IAttackPayload original, IEnumerable<ICombatEffect> effects)
    {
        var copy = new CombatPayload(original.Attacker, original.Source, original.Stats,
            original.AttackSeed, original.SeedProvenance);
        foreach (var effect in effects)
        {
            copy.AddEffect(effect);
        }
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
