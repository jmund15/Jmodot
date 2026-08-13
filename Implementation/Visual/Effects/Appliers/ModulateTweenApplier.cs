namespace Jmodot.Implementation.Visual.Effects.Appliers;

using System;
using Core.Visual.Effects;
using Godot;

/// <summary>
/// Default <see cref="IEffectApplier"/> for Modulate-based tween effects
/// (<see cref="Jmodot.Implementation.Visual.Effects.FlashEffect"/>,
/// <see cref="Jmodot.Implementation.Visual.Effects.TintEffect"/>, etc.). Wraps the
/// <see cref="VisualEffect.ConfigureTween"/> mechanism.
/// </summary>
/// <remarks>
/// The applier owns exactly two Godot objects: the <see cref="Tween"/> and the
/// <see cref="VisualEffectHandle"/>. Both are released by <see cref="End"/>. The
/// tween's <c>Finished</c> signal is routed through the <c>onFinished</c> callback
/// passed to <see cref="Begin"/>, so the controller is notified when a
/// natural completion occurs.
/// </remarks>
public sealed class ModulateTweenApplier : IEffectApplier
{
    private readonly VisualEffect _effect;
    private Tween? _tween;
    private VisualEffectHandle? _handle;

    public ModulateTweenApplier(VisualEffect effect)
    {
        _effect = effect;
    }

    public VisualEffectHandle Begin(SceneTree tree, Action onFinished)
    {
        _handle = new VisualEffectHandle();
        _tween = tree.CreateTween();
        _effect.ConfigureTween(_tween, _handle);
        _tween.Finished += onFinished;
        _tween.Play();
        return _handle;
    }

    public void End()
    {
        if (_tween != null && GodotObject.IsInstanceValid(_tween))
        {
            _tween.Kill();
        }
        _tween = null;

        if (_handle != null && GodotObject.IsInstanceValid(_handle))
        {
            _handle.Free();
        }
        _handle = null;
    }
}
