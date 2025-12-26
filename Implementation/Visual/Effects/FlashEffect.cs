namespace Jmodot.Implementation.Visual.Effects;

using System.Collections.Generic;
using Core.Visual.Effects;
using Godot;

/// <summary>
/// Visual effect that flashes sprites between their original color and a flash color.
/// </summary>
[GlobalClass]
public partial class FlashEffect : VisualEffect
{
    /// <summary>
    /// The color to flash to. Default is white for a "hit flash" effect.
    /// </summary>
    [Export] public Color FlashColor { get; set; } //= Colors.White;

    /// <summary>
    /// Number of complete flash cycles (on-off pairs) during the effect duration.
    /// </summary>
    [Export] public int FlashCount { get; set; } = 4;

    public FlashEffect()
    {
        // Default to Override for Flash effects as they typically demand attention
        // and replacing the color entirely (invincibility/hit frames)
        BlendMode = VisualEffectBlendMode.Override;
    }

    public override void ConfigureTween(Tween tween, VisualEffectHandle handle)
    {
        float totalDuration = Duration;
        float singleFlashDuration = totalDuration / FlashCount;
        float halfFlashDuration = singleFlashDuration / 2;

        for (int i = 0; i < FlashCount; i++)
        {
            // Flash ON
            tween.TweenProperty(handle, "Modulate", FlashColor, halfFlashDuration)
                .SetTrans(Tween.TransitionType.Linear);

            // Flash OFF (Back to White/Input)
            // Note: We tween back to White because the Controller multiplies this value.
            // White = 1.0 = Original Sprite Color.
            tween.TweenProperty(handle, "Modulate", Colors.White, halfFlashDuration)
                .SetTrans(Tween.TransitionType.Linear);
        }
    }
}
