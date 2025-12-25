namespace Jmodot.Core.Visual.Effects;

using System.Collections.Generic;
using Godot;

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

/// <summary>
/// Abstract base class for visual effects that can be applied to sprites via Tweens.
/// Subclasses define specific effect behaviors (flash, tint, shader effects, etc.)
/// </summary>
[GlobalClass]
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
