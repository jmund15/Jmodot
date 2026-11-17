namespace Jmodot.Implementation.Visual.Animation;

using Godot;

/// <summary>
/// Composable tween configuration resource for designer-friendly animation tuning.
/// Encapsulates duration, transition type, and easing in a reusable resource.
///
/// Usage:
/// <code>
/// [Export] public TweenConfig? ShiftConfig { get; set; }
/// // In code:
/// var tweener = config.ApplyTo(tween, targetNode, "scale", Vector2.One);
/// </code>
/// </summary>
[GlobalClass, Tool]
public partial class TweenConfig : Resource
{
    /// <summary>
    /// Duration of the tween in seconds.
    /// </summary>
    [Export] public float Duration { get; set; } = 0.3f;

    /// <summary>
    /// The transition function used for interpolation (e.g., Quad, Bounce, Elastic).
    /// </summary>
    [Export] public Tween.TransitionType Transition { get; set; } = Tween.TransitionType.Quad;

    /// <summary>
    /// The easing direction (In, Out, InOut).
    /// </summary>
    [Export] public Tween.EaseType Ease { get; set; } = Tween.EaseType.Out;

    /// <summary>
    /// Applies this configuration to create a PropertyTweener on the given tween.
    /// </summary>
    /// <param name="tween">The parent Tween to add the tweener to.</param>
    /// <param name="obj">The target object containing the property.</param>
    /// <param name="property">The property path to animate (e.g., "modulate:a", "scale").</param>
    /// <param name="endValue">The target value for the property.</param>
    /// <returns>The configured PropertyTweener for additional chaining if needed.</returns>
    public PropertyTweener ApplyTo(Tween tween, GodotObject obj, string property, Variant endValue)
    {
        return tween
            .TweenProperty(obj, property, endValue, Duration)
            .SetTrans(Transition)
            .SetEase(Ease);
    }
}
