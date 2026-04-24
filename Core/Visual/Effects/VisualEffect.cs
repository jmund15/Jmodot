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

    /// <summary>
    /// Configure the Tween to perform this effect on the handle.
    /// The tween is already created; add your TweenProperty/TweenCallback calls targeting the handle.
    /// </summary>
    /// <param name="tween">The Tween to configure</param>
    /// <param name="handle">The data handle to tween properties on (e.g. handle.Modulate)</param>
    public abstract void ConfigureTween(Tween tween, VisualEffectHandle handle);

    /// <summary>
    /// Produces a runtime <see cref="IEffectApplier"/> for this effect. Modulate-tween
    /// based subclasses return a <c>ModulateTweenApplier</c> wrapping
    /// <see cref="ConfigureTween"/>; future non-Modulate effects (shader glow,
    /// particle burst) return their own applier type.
    /// </summary>
    /// <remarks>
    /// Phase 4.4 — this indirection lets <c>VisualEffectController</c> treat effects
    /// as opaque "start / stop" units instead of assuming every effect is Modulate-tween
    /// based. <b>No default implementation</b> because the default applier (<c>ModulateTweenApplier</c>)
    /// lives in Implementation — Core has no business referencing it.
    /// </remarks>
    public abstract IEffectApplier CreateApplier();

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
