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
    /// Fired when the animation finishes and the effect is about to be freed.
    /// </summary>
    public event Action<OneShotAnimEffect>? EffectFinished;

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

    private IAnimComponent? _animComponent;
    private bool _hasFinished;

    public override void _Ready()
    {
        _animComponent = this as IAnimComponent;
        _animComponent ??= this.GetFirstChildOfInterface<IAnimComponent>();

        if (_animComponent != null)
        {
            _animComponent.AnimFinished += OnAnimFinished;
        }
    }

    /// <summary>
    /// Plays the specified animation by name.
    /// </summary>
    public void Play(StringName animName)
    {
        _animComponent?.StartAnim(animName);
    }

    /// <summary>
    /// Selects a random animation from available variants and plays it.
    /// Uses the provided RNG for deterministic seeded results.
    /// </summary>
    public void PlayRandom(Random rng)
    {
        var anims = _animComponent?.GetAnimationList();
        if (anims == null || anims.Length == 0)
        {
            OnAnimFinished("");
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
    /// Called when the effect finishes. Override to add custom cleanup behavior.
    /// Base implementation fires EffectFinished and calls QueueFree.
    /// </summary>
    protected virtual void OnEffectFinished()
    {
        EffectFinished?.Invoke(this);
        if (AutoFree)
        {
            if (OnFinishDestroyStrategy != null)
            {
                OnFinishDestroyStrategy.Destroy(this, () => { });
            }
            else
            {
                QueueFree();
            }
        }
    }

    private void OnAnimFinished(StringName _)
    {
        if (_hasFinished) { return; }
        _hasFinished = true;
        OnEffectFinished();
    }
}
