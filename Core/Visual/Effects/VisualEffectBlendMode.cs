namespace Jmodot.Core.Visual.Effects;

/// <summary>
/// Controls how multiple visual effects interact when active simultaneously.
/// </summary>
public enum VisualEffectBlendMode
{
    /// <summary>
    /// The effect's modulation is multiplied with other active Mix effects.
    /// (e.g. Red tint * Blue tint = Purple tint).
    /// </summary>
    Mix,

    /// <summary>
    /// The effect completely overrides all other effects while active.
    /// If multiple Overrides are active, the one with higher Priority (or newer) wins.
    /// </summary>
    Override
}
