namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System;
using Core.Visual.Animation.Sprite;

using Godot;
using Core.Visual.Sprite;

/// <summary>
/// An IAnimComponent and ISpriteComponent implementation that directly inherits from AnimatedSprite3D.
/// This component is designed to be added directly to a scene as a node. It combines the functionality
/// of Godot's AnimatedSprite3D with the contracts required by the Jmodot animation system.
/// This approach is simpler for direct scene setup but less flexible than the composition-based wrapper.
/// </summary>
[GlobalClass]
public partial class AnimatedSprite3DComponent : AnimatedSprite3D, IAnimComponent, ISpriteComponent
{
    // --- IAnimComponent Events ---
    public event Action<StringName> AnimStarted;
    public event Action<StringName> AnimFinished;

    public override void _Ready()
    {
        base._Ready();

        // Connect the base class's signals to our interface's events.
        // AnimationChanged is a reliable signal that fires as soon as Play() is called.
        this.AnimationChanged += () => AnimStarted?.Invoke(this.Animation);
        this.AnimationFinished += () => AnimFinished?.Invoke(this.Animation);
    }

    // --- IAnimComponent Implementation ---

    public void StartAnim(StringName animName)
    {
        // Add a check for robustness to prevent Godot errors for non-existent animations.
        if (this.SpriteFrames.HasAnimation(animName))
        {
            this.Play(animName);
        }
        else
        {
            GD.PrintErr($"Animation '{animName}' not found in SpriteFrames for node '{this.Name}'.");
        }
    }

    public void UpdateAnim(StringName animName)
    {
        // If the requested animation is already playing, do nothing.
        if (this.Animation == animName) return;

        if (this.SpriteFrames.HasAnimation(animName))
        {
            // Preserve the frame index to maintain visual continuity when switching animations.
            var currentFrame = this.Frame;
            this.Play(animName);
            this.Frame = currentFrame;
        }
        else
        {
            GD.PrintErr($"Cannot update to animation '{animName}': not found in SpriteFrames for node '{this.Name}'.");
        }
    }

    public void PauseAnim() => this.Pause();
    public void StopAnim() => this.Stop();
    public bool IsPlaying() => this.IsPlaying();
    public bool HasAnimation(StringName animName) => this.SpriteFrames.HasAnimation(animName);
    public StringName GetCurrAnimation() => this.Animation;
    public float GetSpeedScale() => this.SpeedScale;
    public void SetSpeedScale(float speedScale) => this.SpeedScale = speedScale;

    // --- ISpriteComponent Implementation ---

    public float GetSpriteHeight()
    {
        // The size can vary per frame, so we get the texture of the current frame.
        if (SpriteFrames == null || string.IsNullOrEmpty(Animation)) return 0f;
        var texture = SpriteFrames.GetFrameTexture(Animation, Frame);
        return texture?.GetHeight() * PixelSize * Scale.Y ?? 0f;
    }

    public float GetSpriteWidth()
    {
        if (SpriteFrames == null || string.IsNullOrEmpty(Animation)) return 0f;
        var texture = SpriteFrames.GetFrameTexture(Animation, Frame);
        return texture?.GetWidth() * PixelSize * Scale.X ?? 0f;
    }

    public Texture2D GetTexture()
    {
        if (SpriteFrames == null || string.IsNullOrEmpty(Animation)) return null;
        return SpriteFrames.GetFrameTexture(Animation, Frame);
    }

    // Note: The properties FlipH, FlipV, and Offset are already part of the base Sprite3D class,
    // so the interface contract is fulfilled automatically by inheritance.

    public Node GetUnderlyingNode() => this;
}
