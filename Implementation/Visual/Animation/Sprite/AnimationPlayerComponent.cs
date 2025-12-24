namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Godot;
using System;
using Core.Visual.Animation.Sprite;

[Tool]
[GlobalClass]
public partial class AnimationPlayerComponent : AnimationPlayer, IAnimComponent
{
    public event Action<StringName> AnimStarted;
    public event Action<StringName> AnimFinished;

    public override void _Ready()
    {
        AnimationStarted += animName => AnimStarted?.Invoke(animName);
        AnimationFinished += animName => AnimFinished?.Invoke(animName);
    }

    public void StartAnim(StringName animName)
    {
        if (HasAnimation(animName))
        {
            Play(animName);
        }
        else
        {
            GD.PrintErr($"Animation '{animName}' not found in AnimationPlayer '{Name}'.");
        }
    }
    public void StopAnim() => this.Stop();
    public void UpdateAnim(StringName animName, AnimUpdateMode mode = AnimUpdateMode.MaintainTime)
    {
        if (CurrentAnimation == animName && mode != AnimUpdateMode.Reset) return;

        if (HasAnimation(animName))
        {
            // Capture state
            double currentPos = CurrentAnimationPosition;
            double currentLen = CurrentAnimationLength;
            double normalizedPos = (currentLen > 0) ? (currentPos / currentLen) : 0.0;

            Play(animName);

            switch (mode)
            {
                case AnimUpdateMode.Reset:
                    // Play defaults to start
                    break;

                case AnimUpdateMode.MaintainTime:
                    Seek(currentPos, true);
                    break;

                case AnimUpdateMode.MaintainPercent:
                    double newLen = CurrentAnimationLength;
                    if (newLen > 0)
                    {
                        Seek(normalizedPos * newLen, true);
                    }
                    break;
            }
        }
        else
        {
            // Don't stop a valid animation just because the new one doesn't exist.
            GD.PrintErr($"Cannot update to animation '{animName}': not found in AnimationPlayer '{Name}'.");
        }
    }

    // // Since AnimationPlayer names are StringName, we need this override
    // public new bool HasAnimation(string animName)
    // {
    //     return base.HasAnimation(animName);
    // }

    public void SeekPos(float time, bool updateNow = true) => Seek(time, updateNow);
    public void FastForward(float time) => Advance(time);
    public StringName GetCurrAnimation() => CurrentAnimation;
    public float GetCurrAnimationLength() => (float)CurrentAnimationLength;
    public float GetAnimationLength(StringName animName)
    {
        if (HasAnimation(animName))
        {
            var anim = GetAnimation(animName);
            return (float)anim.Length;
        }
        return 0f;
    }
    public float GetCurrAnimationPosition() => (float)CurrentAnimationPosition;
    //public float GetSpeedScale() => (float)SpeedScale;
    public new void SetSpeedScale(float speedScale) => SpeedScale = speedScale; // 'new' keyword to hide base member
    public new void PauseAnim() => Pause(); // Map interface to concrete implementation
    public Node GetUnderlyingNode() => this;
}
