namespace Jmodot.Implementation.Visual.Effects;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.Visual;
using Jmodot.Core.Visual.Effects;
using Shared;

/// <summary>
/// Concrete <see cref="IVisualEffectService"/>. Replaces the legacy
/// <c>BaseModulationTracker</c> with a query-driven, hot-swap-aware tint surface.
/// </summary>
/// <remarks>
/// Authored as a <c>Node</c> so it can be a scene-graph child of <c>VisualComposer</c>
/// and exported via <c>[Export]</c>. The composer wires its provider events into the
/// service via <see cref="AttachToProvider"/> on <c>_Ready</c>; this is a one-way
/// inversion (service learns about provider, not vice versa) that breaks the
/// circular [Export] dependency that would otherwise occur.
/// </remarks>
[GlobalClass, Tool]
public partial class VisualEffectService : Node, IVisualEffectService
{
    private readonly Dictionary<Node, Color> _baseColors = new();
    private readonly Dictionary<EffectId, (VisualQuery query, Color color)> _persistentTints = new();
    private long _nextEffectId = 1;

    private IVisualNodeProvider? _provider;

    public event Action<Node, Color> TintChanged = delegate { };

    /// <summary>
    /// Wires the service to its source of <see cref="VisualNodeHandle"/> events.
    /// Called by <c>VisualComposer._Ready</c>. Idempotent; re-attaching unhooks
    /// the previous provider first.
    /// </summary>
    public void AttachToProvider(IVisualNodeProvider provider)
    {
        if (_provider != null)
        {
            _provider.NodeAdded -= OnNodeAdded;
            _provider.NodeRemoved -= OnNodeRemoved;
        }
        _provider = provider;
        provider.NodeAdded += OnNodeAdded;
        provider.NodeRemoved += OnNodeRemoved;
    }

    public override void _ExitTree()
    {
        if (_provider != null)
        {
            _provider.NodeAdded -= OnNodeAdded;
            _provider.NodeRemoved -= OnNodeRemoved;
            _provider = null;
        }
    }

    public void RegisterBaseColor(Node node, Color color)
    {
        _baseColors[node] = color;
        TintChanged?.Invoke(node, color);
    }

    public void UnregisterSprite(Node node)
    {
        _baseColors.Remove(node);
    }

    public Color GetBaseColor(Node node)
        => _baseColors.TryGetValue(node, out var c) ? c : Colors.White;

    public bool TryGetBaseColor(Node node, out Color baseColor)
        => _baseColors.TryGetValue(node, out baseColor);

    public EffectId TintByQuery(VisualQuery query, Color color)
    {
        var id = new EffectId(_nextEffectId++);
        _persistentTints[id] = (query, color);

        if (_provider != null)
        {
            foreach (var handle in _provider.GetVisualNodes(query))
            {
                ApplyTint(handle.Node, color);
            }
        }
        return id;
    }

    public void RemoveTint(EffectId id)
    {
        if (!_persistentTints.TryGetValue(id, out var entry)) { return; }
        _persistentTints.Remove(id);

        if (_provider != null)
        {
            foreach (var handle in _provider.GetVisualNodes(entry.query))
            {
                // Revert to whatever base color is still registered (or White).
                var baseColor = GetBaseColor(handle.Node);
                ApplyTint(handle.Node, baseColor);
            }
        }
    }

    private void OnNodeAdded(VisualNodeHandle handle)
    {
        // Re-apply any persistent tint whose query matches this new handle.
        foreach (var (_, entry) in _persistentTints)
        {
            if (entry.query.Matches(handle))
            {
                ApplyTint(handle.Node, entry.color);
            }
        }
    }

    private void OnNodeRemoved(VisualNodeHandle handle)
    {
        // Service-side bookkeeping — VisualSlotNode also calls UnregisterSprite,
        // but defense-in-depth: clean up if a removal slips through.
        _baseColors.Remove(handle.Node);
    }

    private void ApplyTint(Node node, Color color)
    {
        if (!GodotObject.IsInstanceValid(node)) { return; }

        switch (node)
        {
            case Sprite2D s2: s2.Modulate = color; break;
            case CanvasItem ci: ci.Modulate = color; break;
            case SpriteBase3D s3d: s3d.Modulate = color; break;
        }
        TintChanged?.Invoke(node, color);
    }
}
