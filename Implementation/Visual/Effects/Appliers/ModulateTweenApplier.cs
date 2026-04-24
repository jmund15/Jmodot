namespace Jmodot.Implementation.Visual.Effects.Appliers;

using System;
using Core.Visual.Effects;
using Godot;

/// <summary>
/// Default <see cref="IEffectApplier"/> for Modulate-based tween effects
/// (<c>FlashEffect</c>, <c>TintEffect</c>, etc.). Wraps the existing
/// <see cref="VisualEffect.ConfigureTween"/> mechanism — behavior is identical to
/// the pre-4.4 inline tween setup in <c>VisualEffectController.PlayEffect</c>.
/// </summary>
/// <remarks>
/// The applier owns exactly two Godot objects: the <see cref="Tween"/> and the
/// <see cref="VisualEffectHandle"/>. Both are released by <see cref="End"/>. The
/// tween's <c>Finished</c> signal is routed through <paramref name="onFinished"/>
/// (passed to <see cref="Begin"/>) so the controller gets notified when a
/// natural completion occurs.
/// </remarks>
public sealed class ModulateTweenApplier : IEffectApplier
{
    private readonly VisualEffect _effect;
    private Tween? _tween;
    private VisualEffectHandle? _handle;

    public ModulateTweenApplier(VisualEffect effect)
    {
        this._effect = effect;
    }

    public VisualEffectHandle Begin(SceneTree tree, Action onFinished)
    {
        this._handle = new VisualEffectHandle();
        this._tween = tree.CreateTween();
        this._effect.ConfigureTween(this._tween, this._handle);
        this._tween.Finished += onFinished;
        this._tween.Play();
        return this._handle;
    }

    public void End()
    {
        if (this._tween != null && GodotObject.IsInstanceValid(this._tween))
        {
            this._tween.Kill();
        }
        this._tween = null;

        if (this._handle != null && GodotObject.IsInstanceValid(this._handle))
        {
            this._handle.Free();
        }
        this._handle = null;
    }
}
