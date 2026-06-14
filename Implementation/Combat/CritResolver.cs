namespace Jmodot.Implementation.Combat;

/// <summary>
/// Pure-CLR critical-hit predicate, shared by the damage factories (assembly-time, <c>Resolved</c>) and
/// the damage effects (apply-time, <c>DeferredPerHit</c>). The roll is injected as a <c>float</c> so this
/// helper never constructs a <c>JmoRng</c> — its ctor SIGSEGVs the CLR test host, so callers seed a
/// <c>JmoRng</c> from a lineage seed in a Godot-runtime-safe context and pass the drawn roll here.
/// Sibling of <see cref="KnockbackForceResolver"/> / <see cref="SourceAttributionResolver"/>.
/// </summary>
public static class CritResolver
{
    /// <summary>A hit is critical when the drawn <paramref name="roll"/> (in [0,1)) lands below
    /// <paramref name="critChance"/>. Equality is a non-crit, matching the legacy
    /// <c>roll &lt; critChance</c> comparison the factories used pre-migration.</summary>
    public static bool Resolve(float roll, float critChance) => roll < critChance;
}
