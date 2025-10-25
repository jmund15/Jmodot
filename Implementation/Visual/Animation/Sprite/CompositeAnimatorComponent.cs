namespace Jmodot.Implementation.Visual.Animation.Sprite;

// Jmodot/Implementation/Visual/Animation/Sprite/Components/CompositeAnimator.cs
using Godot;
using System.Collections.Generic;
using System.Linq;
using System;
using Core.Visual.Animation.Sprite;
using Core.Visual.Sprite;

[GlobalClass]
public partial class CompositeAnimatorComponent : Node, IAnimComponent
{
    public List<IAnimComponent> Animators { get; private set; } = new();
    public List<ISpriteComponent> Sprites { get; private set; } = new();

    public event Action<StringName> AnimStarted;
    public event Action<StringName> AnimFinished;

    public override void _Ready()
    {
        foreach (var child in GetChildren())
        {
            if (child is IAnimComponent anim) Animators.Add(anim);
            if (child is ISpriteComponent sprite) Sprites.Add(sprite);
        }

        if (!Animators.Any()) { GD.PrintErr("CompositeAnimator has no IAnimComponent children."); return; }

        Animators[0].AnimStarted += animName => AnimStarted?.Invoke(animName);
        Animators[0].AnimFinished += animName => AnimFinished?.Invoke(animName);
    }

    public void StartAnim(StringName animName) => Animators.ForEach(a => a.StartAnim(animName));
    public void UpdateAnim(StringName animName) => Animators.ForEach(a => a.UpdateAnim(animName));
    public void StopAnim() => Animators.ForEach(a => a.StopAnim());
    public void PauseAnim() => Animators.ForEach(a => a.PauseAnim());
    public void SetSpeedScale(float speedScale) => Animators.ForEach(a => a.SetSpeedScale(speedScale));

    public StringName GetCurrAnimation() => Animators.FirstOrDefault()?.GetCurrAnimation() ?? "";
    public bool IsPlaying() => Animators.FirstOrDefault()?.IsPlaying() ?? false;
    public bool HasAnimation(StringName animName) => Animators.FirstOrDefault()?.HasAnimation(animName) ?? false;
    public float GetSpeedScale() => Animators.FirstOrDefault()?.GetSpeedScale() ?? 1.0f;
    public Node GetUnderlyingNode() => this;
}
