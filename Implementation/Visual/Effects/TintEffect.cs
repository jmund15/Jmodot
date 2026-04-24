namespace Jmodot.Implementation.Visual.Effects;

using System.Collections.Generic;
using Appliers;
using Core.Visual.Effects;
using Godot;

/// <summary>
/// Visual effect that tints sprites with a color, fading in and out.
/// Useful for status effects, environmental hazards, etc.
/// </summary>
[GlobalClass, Tool]
public partial class TintEffect : VisualEffect
{
    /// <summary>
    /// The target tint color at peak intensity.
    /// </summary>
    [Export] public Color TintColor { get; set; } = Colors.Red;

    /// <summary>
    /// What portion of the duration is spent fading in (0.0 to 1.0).
    /// The remaining portion is spent holding, then fading out symmetrically.
    /// Example: 0.2 means 20% fade in, 60% hold, 20% fade out.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,0.5,0.05")]
    public float FadeRatio { get; set; } = 0.2f;

    /// <summary>
    /// Absolute fade-in time in seconds. When > 0, overrides FadeRatio for fade-in.
    /// </summary>
    [Export] public float FadeInTime { get; set; } = 0f;

    /// <summary>
    /// Absolute fade-out time in seconds. When > 0, overrides FadeRatio for fade-out.
    /// </summary>
    [Export] public float FadeOutTime { get; set; } = 0f;

    /// <summary>
    /// Resolves the fade-in duration: FadeInTime if set, otherwise Duration * FadeRatio.
    /// </summary>
    public float GetFadeInDuration() => FadeInTime > 0 ? FadeInTime : Duration * FadeRatio;

    /// <summary>
    /// Resolves the fade-out duration: FadeOutTime if set, otherwise Duration * FadeRatio.
    /// </summary>
    public float GetFadeOutDuration() => FadeOutTime > 0 ? FadeOutTime : Duration * FadeRatio;

    public override IEffectApplier CreateApplier() => new ModulateTweenApplier(this);

    public override void ConfigureTween(Tween tween, VisualEffectHandle handle)
    {
        float fadeInDuration = GetFadeInDuration();
        float fadeOutDuration = GetFadeOutDuration();
        float holdDuration = System.Math.Max(0, Duration - fadeInDuration - fadeOutDuration);

        // Fade in to tint color
        tween.TweenProperty(handle, "Modulate", TintColor, fadeInDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);

        // Hold at tint color
        if (holdDuration > 0)
        {
            tween.TweenInterval(holdDuration);
        }

        // Fade out to original (White = 1.0 multiplier)
        tween.TweenProperty(handle, "Modulate", Colors.White, fadeOutDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
    }
}
