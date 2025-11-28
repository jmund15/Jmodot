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

    void StartAnim(StringName animName);
    void PauseAnim();
    void StopAnim();
    /// <summary>
    /// Switch animation, attempting to preserve relative progress if applicable.
    /// </summary>
    void UpdateAnim(StringName animName);
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
    /// Returns current progress in seconds.
    /// For AnimatedSprite: CurrentFrame / FPS.
    /// </summary>
    float GetCurrAnimationPosition();

    /// <summary>
    /// Returns a list of all valid animation names this component supports.
    /// Used for editor tools and validation.
    /// </summary>
    string[] GetAnimationList();
}
