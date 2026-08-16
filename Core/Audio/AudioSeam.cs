namespace Jmodot.Core.Audio;

using Jmodot.Implementation.Audio;

/// <summary>
/// Static seam holding the <see cref="IAudioDirector"/> default. Consuming projects forward
/// their director here at startup; <see cref="SoundEffectComponent"/> reads it lazily at
/// play-time (never cached at init). A null director is the pre-forward window or a Jmodot-only
/// test without a spy — consumers drop the request with a one-time warning: audio is feedback,
/// and a missing director must not take the game down.
/// </summary>
public static class AudioSeam
{
    /// <summary>The active audio director, or null before any project forwards itself.</summary>
    public static IAudioDirector? Director;

    /// <summary>
    /// Clears the director. Intended for test teardown — lets Jmodot-only suites reset shared
    /// static state without depending on a consuming project's autoload reset path.
    /// </summary>
    public static void Reset() => Director = null;
}
