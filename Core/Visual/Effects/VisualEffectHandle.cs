namespace Jmodot.Core.Visual.Effects;

/// <summary>
/// A handle object that represents the running state of a visual effect.
/// This object is what gets tweened by the VisualEffect.
/// </summary>
public partial class VisualEffectHandle : GodotObject
{
    /// <summary>
    /// The current color contribution of this effect.
    /// Defaut is White (no change/identity).
    /// </summary>
    [Export] public Color Modulate { get; set; } = Colors.White;
}
