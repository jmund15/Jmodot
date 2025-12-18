namespace Jmodot.Implementation.Visual.Effects;

using System.Collections.Generic;
using System.Linq;
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

    private string _modulateKey = "Modulate";

    public override Dictionary<string, Variant> CaptureState(Node node)
    {
        return new Dictionary<string, Variant>
        {
            [_modulateKey] = GetModulate(node)
        };
    }

    public override void RestoreState(Node node, Dictionary<string, Variant> state)
    {
        if (state.TryGetValue(_modulateKey, out var modulate))
        {
            SetModulate(node, modulate.AsColor());
        }
    }

    public override void ConfigureTween(Tween tween, List<Node> nodes, float elapsedTime = 0f)
    {
        float halfFlashDuration = Duration / (FlashCount * 2);

        // // Build the flash sequence: flash on, flash off, repeat
        // for (int i = 0; i < FlashCount; i++)
        // {

        // START WITH FLASH ON FOR EACH NODE
        for (int n = 0; n < nodes.Count; n++)
        {
            var node = nodes[n];

            if (n == 0)
            {
                // Flash to FlashColor
                tween.TweenProperty(node, "modulate", FlashColor, halfFlashDuration)
                    .SetTrans(Tween.TransitionType.Linear);
            }
            else
            {
                // Flash to FlashColor
                tween.Parallel().TweenProperty(node, "modulate", FlashColor, halfFlashDuration)
                    .SetTrans(Tween.TransitionType.Linear);
            }
        }

        // THEN FLASH OFF FOR EACH NODE
        // START WITH FLASH ON FOR EACH NODE
        for (int n = 0; n < nodes.Count; n++)
        {
            var node = nodes[n];
            var originalColor = GetModulate(node);

            if (n == 0)
            {
                // Return to original
                tween.TweenProperty(node, "modulate", originalColor, halfFlashDuration)
                    .SetTrans(Tween.TransitionType.Linear);
            }
            else
            {
                // Return to original
                tween.TweenProperty(node, "modulate", originalColor, halfFlashDuration)
                    .SetTrans(Tween.TransitionType.Linear);
            }
        }

        //}
        tween.SetLoops(FlashCount - 1); // -1 because we don't count the first one
        tween.CustomStep(elapsedTime);

    }
}
