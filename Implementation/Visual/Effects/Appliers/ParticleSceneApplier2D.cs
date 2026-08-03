namespace Jmodot.Implementation.Visual.Effects.Appliers;

using System;
using Core.Visual.Effects;
using Godot;

/// <summary>
/// 2D mirror of <see cref="ParticleSceneApplier3D"/>: instances a designer-authored
/// <see cref="GpuParticles2D"/> emitter scene under a target node and tints it.
/// </summary>
public sealed class ParticleSceneApplier2D : IEffectApplier
{
    private readonly Node _target;
    private readonly PackedScene _emitterScene;
    private readonly Color _tint;

    private GpuParticles2D? _emitter;
    private VisualEffectHandle? _handle;
    private int _baseAmount = 1;

    public ParticleSceneApplier2D(Node target, PackedScene emitterScene, Color tint)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(emitterScene);
        _target = target;
        _emitterScene = emitterScene;
        _tint = tint;
    }

    /// <summary>The live emitter, or null before <see cref="Begin"/> / after <see cref="End"/>.</summary>
    public GpuParticles2D? Emitter => _emitter;

    public VisualEffectHandle Begin(SceneTree tree, Action onFinished)
    {
        if (_emitter == null)
        {
            _emitter = _emitterScene.Instantiate<GpuParticles2D>();
            _baseAmount = Mathf.Max(1, _emitter.Amount);
            ApplyTint(_emitter, _tint, _emitterScene);
            _target.AddChild(_emitter);
            _emitter.Emitting = true;
        }

        _handle ??= new VisualEffectHandle();
        return _handle;
    }

    /// <summary>
    /// Re-grades emission density in place — no teardown, no re-acquire. An amount CHANGE does
    /// restart the particle system (Godot reallocates the particle buffer on write), so an unchanged
    /// re-grade is skipped rather than written.
    /// </summary>
    public void SetAmountScale(float scale)
    {
        if (_emitter == null || !GodotObject.IsInstanceValid(_emitter)) { return; }

        int amount = Mathf.Max(1, Mathf.RoundToInt(_baseAmount * scale));
        if (amount == _emitter.Amount) { return; }

        _emitter.Amount = amount;
    }

    public void End()
    {
        if (_emitter != null && GodotObject.IsInstanceValid(_emitter))
        {
            _emitter.Emitting = false;
            _emitter.GetParent()?.RemoveChild(_emitter);
            _emitter.QueueFree();
        }
        _emitter = null;

        if (_handle != null && GodotObject.IsInstanceValid(_handle))
        {
            _handle.Free();
        }
        _handle = null;
    }

    private static void ApplyTint(GpuParticles2D emitter, Color tint, PackedScene emitterScene)
    {
        if (emitter.ProcessMaterial is not ParticleProcessMaterial shared)
        {
            throw new Shared.GodotExceptions.ResourceConfigurationException(
                "The emitter scene's root carries no ParticleProcessMaterial, so it cannot carry a "
                + "per-trait color — every consumer would render in the authored color. Assign a "
                + "ParticleProcessMaterial in the particle editor.",
                emitterScene);
        }

        var owned = (ParticleProcessMaterial)shared.Duplicate();
        owned.Color = tint;
        emitter.ProcessMaterial = owned;
    }
}
