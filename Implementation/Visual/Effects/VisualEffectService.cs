namespace Jmodot.Implementation.Visual.Effects;

using System;
using Godot;
using Jmodot.Implementation.Visual.Animation.Sprite;

/// <summary>
/// Default <see cref="IVisualEffectService"/> implementation. Wraps a
/// <see cref="BaseModulationTracker"/> and a <see cref="VisualComposer"/> so
/// <see cref="SetBaseTint"/> can resolve scopes against the composer's slots
/// while tracker registration + Modulate writes happen in one call.
/// </summary>
/// <remarks>
/// Composition-over-inheritance: this class holds the composer and tracker rather
/// than living inside either one. That keeps the service a thin aggregation unit
/// that can be swapped or mocked in tests, and keeps <c>VisualComposer</c> focused
/// on slot orchestration rather than tint ownership.
/// </remarks>
public sealed class VisualEffectService : IVisualEffectService
{
    private readonly VisualComposer _composer;
    private readonly BaseModulationTracker _tracker;

    public event Action TintChanged = delegate { };

    public VisualEffectService(VisualComposer composer, BaseModulationTracker tracker)
    {
        this._composer = composer;
        this._tracker = tracker;
    }

    public void SetBaseTint(Color color, EffectScope scope)
    {
        var touched = false;
        foreach (var node in scope.Resolve(this._composer))
        {
            this._tracker.RegisterBaseColor(node, color);
            ApplyModulate(node, color);
            touched = true;
        }

        if (touched) { this.TintChanged.Invoke(); }
    }

    public void ClearBaseTint(EffectScope scope)
    {
        var touched = false;
        foreach (var node in scope.Resolve(this._composer))
        {
            this._tracker.UnregisterSprite(node);
            ApplyModulate(node, Colors.White);
            touched = true;
        }

        if (touched) { this.TintChanged.Invoke(); }
    }

    private static void ApplyModulate(Node node, Color color)
    {
        if (node is SpriteBase3D s3d) { s3d.Modulate = color; }
        else if (node is CanvasItem ci) { ci.Modulate = color; }
    }
}
