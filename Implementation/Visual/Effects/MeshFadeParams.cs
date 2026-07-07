namespace Jmodot.Implementation.Visual.Effects;

using Godot;

/// <summary>
/// Tuning for the mesh family of a visual fade: every MeshInstance3D descendant
/// tweens its GeometryInstance3D transparency toward <see cref="TargetTransparency"/>
/// (per-instance — no shared-material mutation). Extend here (not in the fader)
/// when mesh fades grow new knobs.
/// </summary>
public sealed record MeshFadeParams
{
    public static readonly MeshFadeParams Default = new();

    public Tween.EaseType Ease { get; init; } = Tween.EaseType.In;
    public Tween.TransitionType Transition { get; init; } = Tween.TransitionType.Linear;
    public float TargetTransparency { get; init; } = 1f;
}
