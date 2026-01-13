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
    public event Action<StringName> AnimStarted = delegate { };
    public event Action<StringName> AnimFinished = delegate { };

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

    public void UpdateAnim(StringName animName, AnimUpdateMode mode = AnimUpdateMode.MaintainTime)
    {
        if (this.Animation == animName && mode != AnimUpdateMode.Reset) { return; }
        if (SpriteFrames == null) { return; }

        if (SpriteFrames.HasAnimation(animName))
        {
            // Capture state before switching
            float currentPos = GetCurrAnimationPosition();
            float currentLen = GetCurrAnimationLength();
            float normalizedPos = (currentLen > 0) ? (currentPos / currentLen) : 0f;

            this.Play(animName);

            switch (mode)
            {
                case AnimUpdateMode.Reset:
                    // Play() defaults to frame 0, so we're good.
                    break;

                case AnimUpdateMode.MaintainTime:
                    SeekPos(currentPos);
                    break;

                case AnimUpdateMode.MaintainPercent:
                    float newLen = GetCurrAnimationLength();
                    if (newLen > 0)
                    {
                        SeekPos(normalizedPos * newLen);
                    }
                    break;
            }
        }
        else
        {
            GD.PrintErr($"Cannot update to animation '{animName}': not found in SpriteFrames for node '{this.Name}'.");
        }
    }

    public void PauseAnim() => this.Pause();
    public void StopAnim() => this.Stop();
    bool IAnimComponent.IsPlaying() => base.IsPlaying();
    public bool HasAnimation(StringName animName) => this.SpriteFrames.HasAnimation(animName);
    public StringName GetCurrAnimation() => this.Animation;
    float IAnimComponent.GetSpeedScale() => this.SpeedScale;
    void IAnimComponent.SetSpeedScale(float speedScale) => this.SpeedScale = speedScale;

    // --- Time-Based Synchronization (Frame <-> Time Mapping) ---
    public void SeekPos(float time, bool updateNow = true)
    {
        if (SpriteFrames == null || !SpriteFrames.HasAnimation(Animation)) { return; }

        float fps = (float)SpriteFrames.GetAnimationSpeed(Animation);
        if (fps <= 0) { return; }

        // Convert Time (seconds) -> Frame Index
        int targetFrame = Mathf.FloorToInt(time * fps);
        int maxFrame = SpriteFrames.GetFrameCount(Animation) - 1;

        // Clamp to ensure we don't crash or loop unexpectedly during a seek
        this.Frame = Mathf.Clamp(targetFrame, 0, maxFrame);
    }

    public float GetCurrAnimationLength()
    {
        if (SpriteFrames == null || !SpriteFrames.HasAnimation(Animation)) { return 0f; }

        float fps = (float)SpriteFrames.GetAnimationSpeed(Animation);
        if (fps <= 0) { return 0f; }

        // Length = Total Frames / FPS
        return SpriteFrames.GetFrameCount(Animation) / fps;
    }

    public float GetAnimationLength(StringName animName)
    {
        if (SpriteFrames == null || !SpriteFrames.HasAnimation(animName)) { return 0f; }

        float fps = (float)SpriteFrames.GetAnimationSpeed(animName);
        if (fps <= 0) { return 0f; }

        return SpriteFrames.GetFrameCount(animName) / fps;
    }

    public float GetCurrAnimationPosition()
    {
        if (SpriteFrames == null || !SpriteFrames.HasAnimation(Animation)) { return 0f; }

        float fps = (float)SpriteFrames.GetAnimationSpeed(Animation);
        if (fps <= 0) { return 0f; }

        // Position = Current Frame / FPS
        return this.Frame / fps;
    }
    public string[] GetAnimationList()
    {
        return SpriteFrames.GetAnimationNames();
    }

    // --- ISpriteComponent Implementation ---

    public float GetSpriteHeight()
    {
        // The size can vary per frame, so we get the texture of the current frame.
        if (SpriteFrames == null || string.IsNullOrEmpty(Animation)) { return 0f; }
        var texture = SpriteFrames.GetFrameTexture(Animation, Frame);
        return texture?.GetHeight() * PixelSize * Scale.Y ?? 0f;
    }

    public float GetSpriteHalfHeight() => GetSpriteHeight() / 2f;

    public float GetSpriteWidth()
    {
        if (SpriteFrames == null || string.IsNullOrEmpty(Animation)) { return 0f; }
        var texture = SpriteFrames.GetFrameTexture(Animation, Frame);
        return texture?.GetWidth() * PixelSize * Scale.X ?? 0f;
    }

    public Texture2D? GetTexture()
    {
        if (SpriteFrames == null || string.IsNullOrEmpty(Animation)) { return null; }
        return SpriteFrames.GetFrameTexture(Animation, Frame);
    }

    // Note: The properties FlipH, FlipV, and Offset are already part of the base Sprite3D class,
    // so the interface contract is fulfilled automatically by inheritance.

    public Node GetUnderlyingNode() => this;
}
