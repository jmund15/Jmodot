namespace Jmodot.Implementation.Visual.Effects;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Visual.Animation.Sprite;

/// <summary>
/// Default <see cref="IVisualEffectService"/> implementation. Owns the per-node
/// base-color dictionary that was previously in <c>BaseModulationTracker</c> —
/// folding those responsibilities into the service is what makes the facade
/// non-leaky: consumers never need to see two cooperating classes.
/// </summary>
/// <remarks>
/// The service is created eagerly by <see cref="VisualComposer.ConfigureSlots"/>
/// so that <see cref="VisualSlot"/>s can call <see cref="RegisterBaseColor"/> as
/// part of their Equip flow without lazy-init race conditions.
/// </remarks>
public sealed class VisualEffectService : IVisualEffectService
{
    private readonly VisualComposer _composer;
    private readonly Dictionary<Node, Color> _baseColors = new();

    public event Action TintChanged = delegate { };

    public VisualEffectService(VisualComposer composer)
    {
        this._composer = composer;
    }

    #region Scoped (fires TintChanged)

    public void SetBaseTint(Color color, EffectScope scope)
    {
        var touched = false;
        foreach (var node in scope.Resolve(this._composer))
        {
            this._baseColors[node] = color;
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
            this._baseColors.Remove(node);
            ApplyModulate(node, Colors.White);
            touched = true;
        }

        if (touched) { this.TintChanged.Invoke(); }
    }

    #endregion

    #region Low-level (silent — no TintChanged)

    public void RegisterBaseColor(Node node, Color color)
    {
        this._baseColors[node] = color;
    }

    public void UnregisterSprite(Node node)
    {
        this._baseColors.Remove(node);
    }

    public Color GetBaseColor(Node node) =>
        this._baseColors.TryGetValue(node, out var c) ? c : Colors.White;

    public bool TryGetBaseColor(Node node, out Color baseColor) =>
        this._baseColors.TryGetValue(node, out baseColor);

    #endregion

    private static void ApplyModulate(Node node, Color color)
    {
        if (node is SpriteBase3D s3d) { s3d.Modulate = color; }
        else if (node is CanvasItem ci) { ci.Modulate = color; }
    }
}
