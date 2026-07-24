namespace Jmodot.Core.Visual.Effects;

using System;
using Godot;
using Jmodot.Core.Visual.Animation.Sprite;
using Jmodot.Core.Visual.DestroyStrategies;
using Jmodot.Implementation.Shared;

/// <summary>
/// Reusable base class for one-shot animated effects (flash bangs, smoke clouds, dust, etc.).
/// Discovers an IAnimComponent child, plays an animation (specific or random variant),
/// fires EffectFinished on completion, and self-destructs via QueueFree.
/// </summary>
[GlobalClass]
public partial class OneShotAnimEffect : Node3D
{
    /// <summary>
    /// Fired exactly once when the effect's lifecycle has completed — after
    /// <see cref="OnFinishDestroyStrategy"/> has run, or immediately on the fallback path.
    /// </summary>
    public event Action EffectFinished = delegate { };

    /// <summary>
    /// When true (default), automatically handles cleanup after the animation finishes.
    /// Set to false when external code manages the node's lifetime (e.g., manual fade tweens).
    /// </summary>
    [Export]
    public bool AutoFree { get; set; } = true;

    /// <summary>
    /// Optional destroy strategy to use instead of QueueFree when the effect finishes.
    /// When set and AutoFree is true, this strategy handles the node's destruction
    /// (e.g., fade out, shatter). When null, falls back to QueueFree.
    /// </summary>
    [Export]
    public DestroyStrategy? OnFinishDestroyStrategy { get; set; }

    /// <summary>
    /// The animation component discovered in <see cref="_Ready"/> — this node itself when it
    /// implements <see cref="IAnimComponent"/>, otherwise the first such child. Null for
    /// effects with no art to animate.
    /// </summary>
    protected IAnimComponent? AnimComponent { get; private set; }

    private bool _hasFinished;

    public override void _Ready()
    {
        AnimComponent = this as IAnimComponent;
        AnimComponent ??= this.GetFirstChildOfInterface<IAnimComponent>();

        if (AnimComponent != null)
        {
            AnimComponent.AnimFinished += OnAnimFinished;
        }
    }

    /// <summary>
    /// Plays the specified animation by name.
    /// </summary>
    public void Play(StringName animName)
    {
        AnimComponent?.StartAnim(animName);
    }

    /// <summary>
    /// Selects a random animation from available variants and plays it.
    /// Uses the provided RNG for deterministic seeded results.
    /// </summary>
    public void PlayRandom(Random rng)
    {
        var anims = AnimComponent?.GetAnimationList();
        if (anims == null || anims.Length == 0)
        {
            MarkFinished();
            return;
        }

        Play(SelectRandomAnimation(anims, rng));
    }

    /// <summary>
    /// Selects a random animation name from the provided array.
    /// Pure function extracted for testability.
    /// </summary>
    public static string SelectRandomAnimation(string[] animations, Random rng)
    {
        if (animations.Length == 0) { return string.Empty; }
        return animations[rng.Next(animations.Length)];
    }

    /// <summary>
    /// Marks the effect complete and dispatches <see cref="OnEffectFinished"/>, at most once.
    /// Call from any non-animation completion trigger (duration timer, degenerate fallback).
    /// </summary>
    protected void MarkFinished()
    {
        if (_hasFinished) { return; }
        _hasFinished = true;
        OnEffectFinished();
    }

    /// <summary>
    /// Called when the effect finishes. Override to add custom cleanup behavior.
    /// Base implementation runs the destroy strategy (or QueueFree) and fires EffectFinished
    /// once destruction has completed.
    /// </summary>
    protected virtual void OnEffectFinished()
    {
        if (AutoFree && OnFinishDestroyStrategy != null)
        {
            OnFinishDestroyStrategy.Destroy(this, RaiseEffectFinished);
            return;
        }

        RaiseEffectFinished();
        if (AutoFree)
        {
            QueueFree();
        }
    }

    /// <summary>Raises <see cref="EffectFinished"/> — C# forbids derived types from invoking a base-declared event.</summary>
    protected void RaiseEffectFinished()
    {
        EffectFinished.Invoke();
    }

    private void OnAnimFinished(StringName _)
    {
        MarkFinished();
    }
}
