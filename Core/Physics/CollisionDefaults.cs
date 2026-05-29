namespace Jmodot.Core.Physics;

/// <summary>
/// Static seam holding the project-wired <see cref="ICollisionReasonResolver"/>.
/// Consuming projects assign <see cref="ReasonResolver"/> once at startup
/// (typically from a registry autoload); Jmodot itself never assigns it,
/// keeping the framework agnostic to any game's collision vocabulary.
///
/// **Not thread-safe.** Wire once from the main thread at startup before any
/// collision resolution. Null <see cref="ReasonResolver"/> degrades gracefully
/// — consumers fall back to their own classification path.
/// </summary>
public static class CollisionDefaults
{
    public static ICollisionReasonResolver? ReasonResolver { get; set; }

    /// <summary>
    /// Clears the wired resolver. Intended for test teardown so Jmodot-only
    /// suites can reset shared static state. Production code should not call this.
    /// </summary>
    internal static void Reset() => ReasonResolver = null;
}
