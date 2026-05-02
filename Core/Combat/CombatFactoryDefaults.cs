namespace Jmodot.Core.Combat;

using Stats;

/// <summary>
/// Project-wide default attributes consumed by Jmodot damage-effect factories
/// (e.g. <see cref="Jmodot.Implementation.Combat.EffectFactories.DamageEffectFactory"/>)
/// when a factory does not configure its own per-instance override.
///
/// Consuming projects wire these fields once at startup (typically from a central
/// registry autoload). Jmodot itself never assigns them — keeping the framework
/// agnostic to any specific game's stat vocabulary.
///
/// **Not thread-safe.** Wire once from the main thread at startup, before any
/// combat factory <c>Create()</c> call. Reads from background threads (e.g.
/// async asset loaders) can observe torn writes.
///
/// Null-handling: when both the per-factory override AND the seam default are
/// null, crit is disabled (graceful degrade — no exception). The factory's
/// <c>DefaultCritMultiplier</c> literal is used when both multiplier sources
/// are null.
///
/// **Scope note:** This seam covers crit attributes only. Status runner
/// PackedScenes are handled separately via <c>[Export, RequiredExport]</c>
/// on each runner factory (see <c>TickEffectFactory.Runner</c>, etc.) — that
/// stronger Inspector-time enforcement is preferred over a runtime fallback
/// because runners are non-graceful (a missing runner cannot degrade
/// meaningfully; it must be set).
/// </summary>
public static class CombatFactoryDefaults
{
    /// <summary>
    /// Fallback attribute used to read crit chance (0.0 - 1.0) when a factory
    /// does not set <c>CritChanceAttrOverride</c>. Null disables crit globally
    /// unless a factory opts in explicitly.
    /// </summary>
    public static Attribute? DefaultCritChanceAttr;

    /// <summary>
    /// Fallback attribute used to read crit damage multiplier when a factory
    /// does not set <c>CritMultiplierAttrOverride</c>. Null causes factories to
    /// use their literal <c>DefaultCritMultiplier</c> export directly.
    /// </summary>
    public static Attribute? DefaultCritMultiplierAttr;

    /// <summary>
    /// Reaction-resolution seam consumed by <c>HurtboxComponent3D.ProcessHit</c>.
    /// When wired, the hurtbox queries this resolver after <c>CanReceiveHit</c> and
    /// before <c>ProcessPayload</c> to apply project-side reaction outcomes (damage
    /// composition, status cleanse, VFX spawn). Null disables consultation gracefully
    /// — the hurtbox falls through to its pre-A2 path.
    /// </summary>
    public static IReactionResolver? ReactionResolver;

    /// <summary>
    /// Clears every default to null. Intended for test teardown — lets Jmodot-only
    /// test suites reset shared static state without depending on a specific
    /// consuming project's autoload reset path. Production code should not call this.
    /// </summary>
    public static void Reset()
    {
        DefaultCritChanceAttr = null;
        DefaultCritMultiplierAttr = null;
        ReactionResolver = null;
    }
}
