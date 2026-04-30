namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;
using System;
using Shared;

/// <summary>
/// Defines the contract for any object that can play discrete, named animations.
/// This is the core interface for the sprite-based animation system, abstracting
/// away whether the implementation uses AnimationPlayer, AnimatedSprite3D, etc.
/// </summary>
public interface IAnimComponent : IGodotNodeInterface
{
    event Action<StringName> AnimStarted;
    event Action<StringName> AnimFinished;

    /// <summary>
    /// Fires when <see cref="StopAnim"/> is called (i.e. the animation is
    /// terminated externally, not via natural completion). The argument is
    /// the animation name that was active at the moment of stopping, or an
    /// empty <see cref="StringName"/> if nothing was playing.
    /// </summary>
    /// <remarks>
    /// Godot's underlying <c>AnimationPlayer.Stop()</c> does NOT emit
    /// <c>AnimationFinished</c>, so listeners that want to clean up state on
    /// teardown (e.g. <c>AnimationVisibilityCoordinator</c> hiding sprites)
    /// need a dedicated stop signal — that's this event.
    /// </remarks>
    event Action<StringName> AnimStopped;

    void StartAnim(StringName animName);
    void PauseAnim();
    void StopAnim();
    /// <summary>
    /// Switch animation, attempting to preserve relative progress if applicable.
    /// </summary>
    void UpdateAnim(StringName animName, AnimUpdateMode mode = AnimUpdateMode.MaintainTime);
    bool IsPlaying();
    bool HasAnimation(StringName animName);
    StringName GetCurrAnimation();
    float GetSpeedScale();
    void SetSpeedScale(float speedScale);

    /// <summary>
    /// Seeks to a specific time in seconds.
    /// For AnimatedSprite, this maps to: Frame = Time * FPS.
    /// </summary>
    void SeekPos(float time, bool updateNow = true);

    /// <summary>
    /// Returns total duration in seconds.
    /// For AnimatedSprite: FrameCount / FPS.
    /// </summary>
    float GetCurrAnimationLength();

    /// <summary>
    /// Returns total duration of a specific animation in seconds, whether active or not.
    /// </summary>
    float GetAnimationLength(StringName animName);

    /// <summary>
    /// Returns current progress in seconds.
    /// For AnimatedSprite: CurrentFrame / FPS.
    /// </summary>
    float GetCurrAnimationPosition();

    /// <summary>
    /// Returns a list of all valid animation names this component supports.
    /// Used for editor tools and validation.
    /// </summary>
    string[] GetAnimationList();

    /// <summary>
    /// Returns whether the specified animation is configured to loop.
    /// Default returns false (assume oneshot if unknown).
    /// </summary>
    bool IsAnimationLooping(StringName animName) => false;
}
