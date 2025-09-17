namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Godot;
using System;
using Core.Visual.Animation.Sprite;

[GlobalClass]
public partial class AnimationPlayerComponent : AnimationPlayer, IAnimComponent
{
    public event Action<string> AnimStarted;
    public event Action<string> AnimFinished;

    public override void _Ready()
    {
        AnimationStarted += animName => AnimStarted?.Invoke(new StringName(animName));
        AnimationFinished += animName => AnimFinished?.Invoke(new StringName(animName));
    }

    public void StartAnim(string animName)
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
    public void UpdateAnim(string animName)
    {
        if (CurrentAnimation == animName) return;

        if (HasAnimation(animName))
        {
            var currentPos = CurrentAnimationPosition;
            Play(animName);
            Seek(currentPos, true);
        }
        else
        {
            // Don't stop a valid animation just because the new one doesn't exist.
            GD.PrintErr($"Cannot update to animation '{animName}': not found in AnimationPlayer '{Name}'.");
        }
    }

    // Since AnimationPlayer names are StringName, we need this override
    public new bool HasAnimation(string animName)
    {
        return base.HasAnimation(animName);
    }

    public void SeekPos(float time, bool updateNow = true) => Seek(time, updateNow);
    public void FastForward(float time) => Advance(time);
    public string GetCurrAnimation() => CurrentAnimation;
    public float GetCurrAnimationLength() => (float)CurrentAnimationLength;
    public float GetCurrAnimationPosition() => (float)CurrentAnimationPosition;
    public float GetSpeedScale() => (float)SpeedScale;
    public new void SetSpeedScale(float speedScale) => SpeedScale = speedScale; // 'new' keyword to hide base member
    public new void PauseAnim() => Pause(); // Map interface to concrete implementation
    public Node GetUnderlyingNode() => this;
}
