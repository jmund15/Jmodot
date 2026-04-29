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

    /// <summary>
    /// Computes base × ∏(matching persistent tints) for the given node, even when
    /// the node isn't currently held as a typed handle by the provider. Falls back
    /// to <see cref="GetBaseColor"/>'s white default if the node is untracked.
    /// Used by transient-effect runners (e.g. <c>VisualEffectController</c>) to
    /// restore the *layered* color on reset, not just the bare base — without
    /// this, a transient effect's finish would clobber persistent tints.
    /// </summary>
    public Color ComputeEffectiveColorForNode(Node node)
    {
        var color = GetBaseColor(node);
        foreach (var (_, entry) in _persistentTints)
        {
            // Synthesize a minimal handle to test the query; only Node identity
            // and tags are inspectable by typical queries. Tags are unknown here
            // (this entry point is for nodes that aren't tracked as VisualNodeHandle),
            // so we pass an empty tag set. Queries that depend on tags will be
            // conservative (no match) — caller-tracked tag-driven tinting should
            // route through ApplyEffectiveColor(VisualNodeHandle) instead.
            var probe = new VisualNodeHandle(null!, null, _emptyTagSet, node, null!, true);
            if (entry.query.Matches(probe))
            {
                color *= entry.color;
            }
        }
        return color;
    }

    private static readonly System.Collections.Generic.HashSet<StringName> _emptyTagSet = new();

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
                ApplyEffectiveColor(handle);
            }
        }
        return id;
    }

    public void RemoveTint(EffectId id)
    {
        if (!_persistentTints.TryGetValue(id, out var entry)) { return; }
        _persistentTints.Remove(id);

        // Recompute the effective color for every node the removed tint matched —
        // any OTHER persistent tints that also matched these nodes must remain applied.
        // The previous implementation reverted to GetBaseColor() unconditionally, which
        // silently clobbered overlapping tints (e.g., a transient sabotage tint
        // erasing the player color on shared nodes).
        if (_provider != null)
        {
            foreach (var handle in _provider.GetVisualNodes(entry.query))
            {
                ApplyEffectiveColor(handle);
            }
        }
    }

    private void OnNodeAdded(VisualNodeHandle handle)
    {
        // Compute the layered effective color for the new handle in one deterministic
        // pass instead of foreach-applying each matching tint (which previously left
        // the result dependent on dictionary iteration order when N>1 tints matched).
        ApplyEffectiveColor(handle);
    }

    /// <summary>
    /// Effective color = base color × product-of-matching-persistent-tints.
    /// Multiplication is commutative, so registration order does not affect the result
    /// (insertion order of <see cref="_persistentTints"/> is preserved by the runtime
    /// but isn't relied on for correctness — only for traceability when debugging).
    /// </summary>
    private Color ComputeEffectiveColor(VisualNodeHandle handle)
    {
        var color = GetBaseColor(handle.Node);
        foreach (var (_, entry) in _persistentTints)
        {
            if (entry.query.Matches(handle))
            {
                color *= entry.color;
            }
        }
        return color;
    }

    private void ApplyEffectiveColor(VisualNodeHandle handle)
    {
        if (!GodotObject.IsInstanceValid(handle.Node)) { return; }
        ApplyTint(handle.Node, ComputeEffectiveColor(handle));
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
