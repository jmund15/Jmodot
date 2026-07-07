namespace Jmodot.Implementation.Visual.Effects;

using Godot;

/// <summary>
/// Tuning for the sprite family of a visual fade: every SpriteBase3D descendant
/// (covers Sprite3D AND AnimatedSprite3D) tweens its modulate alpha toward
/// <see cref="TargetAlpha"/>. Extend here (not in the fader) when sprite fades
/// grow new knobs.
/// </summary>
public sealed record SpriteFadeParams
{
    public static readonly SpriteFadeParams Default = new();

    public Tween.EaseType Ease { get; init; } = Tween.EaseType.In;
    public Tween.TransitionType Transition { get; init; } = Tween.TransitionType.Linear;
    public float TargetAlpha { get; init; }
}
