namespace Jmodot.Core.Visual.Effects;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Abstract base class for visual effects that can be applied to sprites via Tweens.
/// Subclasses define specific effect behaviors (flash, tint, shader effects, etc.)
/// </summary>
[GlobalClass, Tool]
public abstract partial class VisualEffect : Resource
{
    /// <summary>
    /// Total duration of the effect in seconds.
    /// </summary>
    [Export] public float Duration { get; set; } = 1.0f;

    /// <summary>
    /// Priority for effect override. Higher priority effects replace lower priority ones
    /// ONLY if they are both in Override mode, or if one is Override and limits others.
    /// </summary>
    [Export] public int Priority { get; set; } = 0;

    /// <summary>
    /// How this effect blends with others.
    /// </summary>
    [Export] public VisualEffectBlendMode BlendMode { get; set; } = VisualEffectBlendMode.Mix;

    /// <summary>The colour the sprite turns toward through the emission channel while the effect runs.</summary>
    [Export(PropertyHint.ColorNoAlpha)] public Color EmissionColor { get; set; } = Colors.White;

    /// <summary>How far the sprite turns toward EmissionColor, from none at zero to fully replaced at one.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float EmissionWeight { get; set; } = 0f;

    /// <summary>
    /// Configure the Tween to perform this effect on the handle.
    /// The tween is already created; add your TweenProperty/TweenCallback calls targeting the handle.
    /// </summary>
    /// <param name="tween">The Tween to configure</param>
    /// <param name="handle">The data handle to tween properties on (e.g. handle.Modulate)</param>
    public abstract void ConfigureTween(Tween tween, VisualEffectHandle handle);

    #region Helper Methods for Subclasses

    /// <summary>
    /// Check if a node is a supported visual type.
    /// </summary>
    public static bool IsVisualNode(Node node)
    {
        return node is SpriteBase3D or CanvasItem;
    }

    #endregion
}
