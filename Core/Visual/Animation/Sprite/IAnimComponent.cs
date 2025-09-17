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
    event Action<string> AnimStarted;
    event Action<string> AnimFinished;

    void StartAnim(string animName);
    void PauseAnim();
    void StopAnim();
    void UpdateAnim(string animName);
    bool IsPlaying();
    bool HasAnimation(string animName);
    string GetCurrAnimation();
    float GetSpeedScale();
    void SetSpeedScale(float speedScale);
}
