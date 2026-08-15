namespace Jmodot.Core.Audio;

using Jmodot.Implementation.Audio;

/// <summary>
/// Universal fallback sound profiles the per-entity resolution chain falls back to when an
/// entity does not author its own override (<c>entity.HitSound ?? AudioDefaults.Hit</c>, Death
/// analog). A static seam in the house Defaults family — not a <see cref="Godot.Resource"/> —
/// so pinned resolution tests inject plain fixture profiles without touching the Godot resource
/// cache. Consuming projects forward their authored defaults here at startup.
/// </summary>
public static class AudioDefaults
{
    /// <summary>Universal hit sound; used when an entity has no <c>HitSound</c> override.</summary>
    public static SoundProfile? Hit;

    /// <summary>Universal death sound; used when an entity has no <c>DeathSound</c> override.</summary>
    public static SoundProfile? Death;

    /// <summary>Universal wall-impact sound (authored default; consumed by wall profiles).</summary>
    public static SoundProfile? WallImpact;

    /// <summary>
    /// Clears every default to null. Intended for test teardown — lets Jmodot-only suites reset
    /// shared static state without depending on a consuming project's autoload reset path.
    /// </summary>
    public static void Reset()
    {
        Hit = null;
        Death = null;
        WallImpact = null;
    }
}
