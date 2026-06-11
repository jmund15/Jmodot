namespace Jmodot.Implementation.Shared;

/// <summary>
/// Framework-level seed-derivation kinds — the canonical key strings for combat/AI
/// lineage segments shared across games built on Jmodot. Each <c>const</c> carries a
/// <see cref="SeedStreamKeyAttribute"/> whose key equals the const's own value, so a
/// single reflective sweep can enforce key uniqueness across this registry and the
/// consumer-side stream registry without a parallel attribute.
/// <para>
/// Consume the const directly at derivation sites (e.g.
/// <c>SeedManager.DeriveChild(parentSeed, SeedKinds.Hit, hitIndex)</c>); the attribute
/// exists for the reflective uniqueness sweep, not for runtime lookup.
/// </para>
/// </summary>
public static class SeedKinds
{
    [SeedStreamKey("attack")] public const string Attack = "attack";
    [SeedStreamKey("spawn")] public const string Spawn = "spawn";
    [SeedStreamKey("hit")] public const string Hit = "hit";
    [SeedStreamKey("crit")] public const string Crit = "crit";
    [SeedStreamKey("knockback")] public const string Knockback = "knockback";
    [SeedStreamKey("status_spread")] public const string StatusSpread = "status_spread";
    [SeedStreamKey("reaction")] public const string Reaction = "reaction";
    [SeedStreamKey("spread")] public const string Spread = "spread";
}
