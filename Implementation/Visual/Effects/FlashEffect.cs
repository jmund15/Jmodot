namespace Jmodot.Implementation.Visual.Effects;

using System.Collections.Generic;
using Core.Visual.Effects;
using Godot;

/// <summary>
/// Visual effect that flashes sprites between their original color and a flash color.
/// Used for invincibility frames, damage feedback, etc.
/// </summary>
[GlobalClass]
public partial class FlashEffect : VisualEffect
{
    /// <summary>
    /// The color to flash to. Default is white for a "hit flash" effect.
    /// </summary>
    [Export] public Color FlashColor { get; set; } = Colors.White;

    /// <summary>
    /// Number of complete flash cycles (on-off pairs) during the effect duration.
    /// </summary>
    [Export] public int FlashCount { get; set; } = 4;

    public override Dictionary<string, Variant> CaptureState(Node node)
    {
        return new Dictionary<string, Variant>
        {
            ["Modulate"] = GetModulate(node)
        };
    }

    public override void RestoreState(Node node, Dictionary<string, Variant> state)
    {
        if (state.TryGetValue("Modulate", out var modulate))
        {
            SetModulate(node, modulate.AsColor());
        }
    }

    public override void ConfigureTween(Tween tween, Node node)
    {
        var originalColor = GetModulate(node);
        float halfFlashDuration = Duration / (FlashCount * 2);

        // Build the flash sequence: flash on, flash off, repeat
        for (int i = 0; i < FlashCount; i++)
        {
            // Flash to FlashColor
            tween.TweenProperty(node, "modulate", FlashColor, halfFlashDuration)
                .SetTrans(Tween.TransitionType.Linear);

            // Return to original
            tween.TweenProperty(node, "modulate", originalColor, halfFlashDuration)
                .SetTrans(Tween.TransitionType.Linear);
        }
    }
}
