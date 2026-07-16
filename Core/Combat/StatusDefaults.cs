namespace Jmodot.Core.Combat;

/// <summary>
/// Framework-agnostic seam for the project-default <see cref="CategoryInteractionRegistry"/> consulted by
/// <c>StatusEffectComponent.ProcessCategoryInteractions</c> when a component carries no per-entity
/// <c>InteractionRegistry</c> export. Mirrors <see cref="CombatFactoryDefaults"/>: the consuming game's
/// autoload forwards its registry in once at startup; Jmodot itself never assigns it, keeping the framework
/// agnostic to any specific game's status vocabulary. A per-entity export always wins over this fallback.
///
/// **Not thread-safe.** Wire once from the main thread at startup, before any <c>AddStatus</c> call.
/// </summary>
public static class StatusDefaults
{
    /// <summary>
    /// Fallback category-interaction registry. Null → category interactions are skipped unless a component
    /// sets its own <c>InteractionRegistry</c> export.
    /// </summary>
    public static CategoryInteractionRegistry? InteractionRegistry { get; set; }

    /// <summary>
    /// Clears the seam to null. Intended for test teardown — lets Jmodot-only suites reset shared static
    /// state without depending on a specific consuming project's reset path. Production code should not call this.
    /// </summary>
    internal static void Reset() => InteractionRegistry = null;
}
