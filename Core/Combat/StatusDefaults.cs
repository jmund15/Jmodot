namespace Jmodot.Core.Combat;

using System;

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
    /// Installs <paramref name="registry"/> for the lifetime of the returned scope and restores the
    /// PREVIOUS value on dispose — not null. This is what a test wants:
    /// <c>using var _ = StatusDefaults.Override(reg);</c> leaves the process-global exactly as it
    /// found it, so suites that run afterwards keep whatever registry (or absence of one) they were
    /// written against.
    /// </summary>
    public static IDisposable Override(CategoryInteractionRegistry? registry)
    {
        var scope = new OverrideScope(InteractionRegistry);
        InteractionRegistry = registry;
        return scope;
    }

    /// <summary>
    /// Clears the seam to null for the remainder of the process, disabling category interactions for
    /// every later consumer that has no per-entity <c>InteractionRegistry</c> export. Intended only for
    /// genuine process-end teardown. Tests that need to install a registry temporarily should use
    /// <see cref="Override"/>, which restores the previous value. Production code should not call this.
    /// </summary>
    internal static void Reset() => InteractionRegistry = null;

    private sealed class OverrideScope : IDisposable
    {
        private readonly CategoryInteractionRegistry? _previous;
        private bool _disposed;

        internal OverrideScope(CategoryInteractionRegistry? previous) => this._previous = previous;

        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            InteractionRegistry = this._previous;
        }
    }
}
