namespace Jmodot.Core.AI.Emotions;

/// <summary>
///     A data-driven "tag" Resource representing a type of emotion (e.g., "Fear", "Curiosity",
///     "Greed"). Used as a type-safe key in dictionaries, allowing designers to define new
///     emotion types without changing code. Analogous to <see cref="Affinities.Affinity"/>
///     but semantically distinct: affinities are stable personality traits, emotions are
///     transient runtime reactions.
/// </summary>
[GlobalClass]
public partial class EmotionType : Resource
{
    [Export] public string EmotionName { get; private set; } = "Unnamed Emotion";

    /// <summary>
    /// Color used by <c>DebugEmotionComponent</c> for visualization bars.
    /// Zero-config: each emotion type gets its own color without extra setup.
    /// </summary>
    [Export] public Color DebugColor { get; private set; } = Colors.White;
}
