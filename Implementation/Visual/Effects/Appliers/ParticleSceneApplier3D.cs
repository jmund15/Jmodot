namespace Jmodot.Implementation.Visual.Effects.Appliers;

using System;
using Core.Visual.Effects;
using Godot;

/// <summary>
/// Instances a designer-authored <see cref="GpuParticles3D"/> emitter scene under a target node and
/// tints it from a caller-supplied color.
/// </summary>
/// <remarks>
/// The emitter's full particle surface (process material, draw passes, visibility AABB) is authored
/// in the scene, never here — code that half-configures a particle node produces a silently empty
/// emitter. The applier owns the instanced node and its duplicated process material; whoever
/// constructs the applier is its sole owner and must call <see cref="End"/>.
/// </remarks>
public sealed class ParticleSceneApplier3D : IEffectApplier
{
    private readonly Node _target;
    private readonly PackedScene _emitterScene;
    private readonly Color _tint;

    private GpuParticles3D? _emitter;
    private VisualEffectHandle? _handle;
    private int _baseAmount = 1;

    public ParticleSceneApplier3D(Node target, PackedScene emitterScene, Color tint)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(emitterScene);
        _target = target;
        _emitterScene = emitterScene;
        _tint = tint;
    }

    /// <summary>The live emitter, or null before <see cref="Begin"/> / after <see cref="End"/>.</summary>
    public GpuParticles3D? Emitter => _emitter;

    public VisualEffectHandle Begin(SceneTree tree, Action onFinished)
    {
        if (_emitter == null)
        {
            _emitter = _emitterScene.Instantiate<GpuParticles3D>();
            _baseAmount = Mathf.Max(1, _emitter.Amount);
            ApplyTint(_emitter, _tint, _emitterScene);
            _target.AddChild(_emitter);
            _emitter.Emitting = true;
        }

        _handle ??= new VisualEffectHandle();
        return _handle;
    }

    /// <summary>
    /// Re-grades emission density in place. Convergent: no teardown, no re-acquire, so a strength
    /// change never re-acquires the channel's identity. An amount CHANGE does restart the particle
    /// system — Godot reallocates the particle buffer on write — so an unchanged re-grade is skipped
    /// rather than written.
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

    private static void ApplyTint(GpuParticles3D emitter, Color tint, PackedScene emitterScene)
    {
        if (emitter.ProcessMaterial is not ParticleProcessMaterial shared)
        {
            throw new Shared.GodotExceptions.ResourceConfigurationException(
                "The emitter scene's root carries no ParticleProcessMaterial, so it cannot carry a "
                + "per-trait color — every consumer would render in the authored color. Assign a "
                + "ParticleProcessMaterial in the particle editor.",
                emitterScene);
        }

        // Sub-resources of a PackedScene are shared across every instance unless flagged
        // local-to-scene, so recoloring the loaded material would recolor every other entity's
        // emitter built from the same scene.
        var owned = (ParticleProcessMaterial)shared.Duplicate();
        owned.Color = tint;
        emitter.ProcessMaterial = owned;
    }
}
