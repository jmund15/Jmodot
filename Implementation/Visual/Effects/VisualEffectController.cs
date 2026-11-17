namespace Jmodot.Implementation.Visual.Effects;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AI.BB;
using Core.AI.BB;
using Core.Visual;
using Core.Visual.Effects;
using Godot;
using Implementation.Visual.Animation.Sprite;
using Jmodot.Core.Components;
using Shared;

/// <summary>
/// Central controller for transient visual effects (flash, tint, etc.) over the
/// active sprite set of an entity. Composes effect colors via blend modes against
/// per-node base colors owned by the entity's <see cref="VisualEffectService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Wired via explicit <see cref="Composer"/> export (replaces the legacy Blackboard
/// auto-wire of <c>BaseModulationTracker</c> for the composer dependency). The
/// <see cref="Provision"/> below re-references <see cref="BBDataSig"/> deliberately: publishing
/// self under a key is the framework-wide IBlackboardProvider contract every component
/// shares — distinct from the retired pattern of PULLING wiring dependencies off the BB.
/// </para>
/// <para>
/// When a composer is set: subscribes to its <see cref="IVisualNodeProvider.NodeAdded"/>
/// / <see cref="IVisualNodeProvider.NodeRemoved"/> events for node tracking, and to
/// <see cref="IVisualEffectService.TintChanged"/> for base-color updates that follow
/// equipment swaps.
/// </para>
/// <para>
/// When only <see cref="Root"/> is set (single-sprite props with no composer):
/// uses <see cref="VisualNodeAggregator.CollectSprites(Node)"/> to find every sprite under
/// the root and captures their current Modulate as their base color.
/// </para>
/// <para>
/// Composition is per node. Among the active effects that reach a node (<see cref="VisualEffect.Scope"/> null,
/// or matching the node's <see cref="VisualNodeHandle"/>), the best Override — highest
/// <see cref="VisualEffect.Priority"/>, the later play on a tie — supplies the node's modulate and emission; with no
/// Override, Mix effects multiply modulate and combine emissions. A Root-scanned node has no handle, so only an
/// unscoped effect reaches it.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class VisualEffectController : Node, IComponent, IBlackboardProvider
{
    public (StringName Key, object Value)? Provision => (BBDataSig.VisualEffectController, this);

    /// <summary>
    /// Primary source of visual nodes and base colors. When set, the controller
    /// queries the composer for nodes and uses its <see cref="VisualEffectService"/>
    /// for base-color resolution.
    /// </summary>
    [Export] public VisualComposer? Composer { get; set; }

    /// <summary>
    /// Fallback root for single-sprite props (no composer). All
    /// <see cref="SpriteBase3D"/> / <see cref="Sprite2D"/> descendants of this node
    /// are tracked, with current Modulate captured as the base color.
    /// </summary>
    [Export] public Node? Root { get; set; }

    private readonly Dictionary<VisualEffect, ActiveEffectHandle> _activeEffects = new();

    /// <summary>
    /// Tracked nodes and their base modulate colors. Authoritative for the controller's
    /// per-frame blend pass.
    /// </summary>
    private readonly Dictionary<Node, Color> _nodeBaseModulates = new();

    /// <summary>Every node with an emission channel, and the emission last written to it.</summary>
    private readonly Dictionary<Node, Color> _channelNodes = new();

    /// <summary>The composer handle behind each tracked node; a Root-scanned node has none.</summary>
    private readonly Dictionary<Node, VisualNodeHandle> _handles = new();

    private readonly List<Color> _mixEmissionsScratch = new();
    private readonly List<(Node Node, Color Emission)> _emissionWritesScratch = new();
    private ulong _playCount;

    private IVisualEffectService? _subscribedService;

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    public override void _Ready()
    {
        base._Ready();
        SetProcess(false);

        if (Engine.IsEditorHint())
        {
            RefreshVisualNodes();
            return;
        }

        SubscribeToComposer();
        RefreshVisualNodes();
    }

    /// <summary>
    /// Re-attaches composer/service subscriptions after a reparent: _ExitTree tears them down and
    /// _Ready does not run a second time, so _EnterTree is the only hook that fires again.
    /// </summary>
    public override void _EnterTree()
    {
        base._EnterTree();
        if (Engine.IsEditorHint() || !IsNodeReady()) { return; }
        SubscribeToComposer();
    }

    private void SubscribeToComposer()
    {
        if (Composer == null) { return; }

        Composer.NodeAdded -= OnNodeAdded;
        Composer.NodeAdded += OnNodeAdded;
        Composer.NodeRemoved -= OnNodeRemoved;
        Composer.NodeRemoved += OnNodeRemoved;

        if (_subscribedService != null) { _subscribedService.TintChanged -= OnTintChanged; }
        _subscribedService = Composer.Effects;
        if (_subscribedService != null)
        {
            _subscribedService.TintChanged += OnTintChanged;
        }
    }

    /// <summary>
    /// Retained for <see cref="IComponent"/> compliance. The legacy Blackboard
    /// auto-wire is gone — use the <see cref="Composer"/> export instead.
    /// </summary>
    public bool Initialize(IBlackboard bb)
    {
        // Refresh in case the composer's slots equipped after our _Ready.
        RefreshVisualNodes();
        IsInitialized = true;
        Initialized();
        return true;
    }

    public void OnPostInitialize() { }

    public override void _ExitTree()
    {
        base._ExitTree();
        StopAllEffects();
        if (Composer != null)
        {
            Composer.NodeAdded -= OnNodeAdded;
            Composer.NodeRemoved -= OnNodeRemoved;
        }
        if (_subscribedService != null)
        {
            _subscribedService.TintChanged -= OnTintChanged;
            _subscribedService = null;
        }
    }

    public override void _Process(double delta) => ApplyEffects();

    #region Public API

    public void PlayEffect(VisualEffect effect)
    {
        if (effect == null)
        {
            JmoLogger.Warning(this, "Attempted to play null effect");
            return;
        }

        if (_activeEffects.ContainsKey(effect)) { StopEffect(effect); }

        // The applier owns tween and handle lifetime; the controller alone writes the channels.
        var applier = new Appliers.ModulateTweenApplier(effect);
        var stateHandle = applier.Begin(GetTree(), () => OnEffectFinished(effect));

        var handle = new ActiveEffectHandle
        {
            Effect = effect,
            Applier = applier,
            State = stateHandle,
            PlayOrder = ++_playCount,
        };
        _activeEffects[effect] = handle;

        SetProcess(true);
        ApplyEffects();
    }

    public void StopEffect(VisualEffect effect)
    {
        if (effect == null || !_activeEffects.TryGetValue(effect, out var handle)) { return; }

        handle.Applier.End();
        _activeEffects.Remove(effect);

        if (_activeEffects.Count == 0)
        {
            ResetVisuals();
            SetProcess(false);
        }
        else
        {
            ApplyEffects();
        }
    }

    public void StopAllEffects()
    {
        var effects = _activeEffects.Keys.ToList();
        foreach (var effect in effects) { StopEffect(effect); }
    }

    public bool IsEffectPlaying(VisualEffect effect)
        => effect != null && _activeEffects.ContainsKey(effect);

    /// <summary>
    /// Re-scan tracked nodes and refresh base colors. Called automatically on slot
    /// changes via composer events; can be invoked manually if external code mutates
    /// the visual hierarchy.
    /// </summary>
    public void RefreshVisualNodes()
    {
        var collected = CollectCurrentNodes();
        var current = new HashSet<Node>(collected.Keys);
        _handles.Clear();
        foreach (var (node, handle) in collected)
        {
            if (handle != null) { _handles[node] = handle; }
        }
        foreach (var staleNode in _channelNodes.Keys.Where(n => !GodotObject.IsInstanceValid(n) || !current.Contains(n)).ToArray())
        {
            _channelNodes.Remove(staleNode);
            if (GodotObject.IsInstanceValid(staleNode)) { WriteEmission(staleNode, EffectEmission.None); }
        }
        foreach (var node in current)
        {
            if (node is not GeometryInstance3D and not CanvasItem || _channelNodes.ContainsKey(node)) { continue; }
            TrackChannel(node);
        }
        var written = new HashSet<Node>(current);
        current.RemoveWhere(node => VisualNodeAggregator.InheritsModulate(node, written.Contains));

        // Drop nodes no longer present (or freed).
        var stale = _nodeBaseModulates.Keys.Where(n => !GodotObject.IsInstanceValid(n) || !current.Contains(n)).ToList();
        foreach (var n in stale) { Untrack(n); }

        var service = Composer?.Effects;
        foreach (var node in current)
        {
            if (service != null)
            {
                _nodeBaseModulates[node] = service.GetBaseColor(node);
            }
            else if (!_nodeBaseModulates.ContainsKey(node))
            {
                _nodeBaseModulates[node] = GetModulate(node);
            }
        }
    }

    #endregion

    #region Internal

    private void OnNodeAdded(VisualNodeHandle h)
    {
        // Incremental update: the event already carries the affected handle, so we
        // don't need the full RefreshVisualNodes scan (which previously ran once per
        // handle of every multi-binding rig — O(n²) per equip).
        if (!GodotObject.IsInstanceValid(h.Node)) { return; }
        _handles[h.Node] = h;
        if (h.Node is GeometryInstance3D or CanvasItem && !_channelNodes.ContainsKey(h.Node))
        {
            TrackChannel(h.Node);
        }
        if (VisualNodeAggregator.InheritsModulate(h.Node, _nodeBaseModulates.ContainsKey)) { return; }
        var service = Composer?.Effects;
        _nodeBaseModulates[h.Node] = service != null ? service.GetBaseColor(h.Node) : GetModulate(h.Node);

        // A descendant that arrived before this ancestor now inherits its colour through the engine.
        var nested = _nodeBaseModulates.Keys
            .Where(n => GodotObject.IsInstanceValid(n) && h.Node.IsAncestorOf(n)
                && VisualNodeAggregator.InheritsModulate(n, _nodeBaseModulates.ContainsKey))
            .ToList();
        foreach (var n in nested) { Untrack(n); }
    }

    private void TrackChannel(Node node)
    {
        var emission = Compose(node).Emission;
        _channelNodes[node] = emission;
        WriteEmission(node, emission);
    }

    // Writes the node's tracked base colour back before dropping it, so an active effect never stays baked into it.
    private void Untrack(Node node)
    {
        if (!_nodeBaseModulates.Remove(node, out var baseColor) || !GodotObject.IsInstanceValid(node)) { return; }
        SetModulate(node, baseColor);
    }

    private void OnNodeRemoved(VisualNodeHandle h)
    {
        if (_channelNodes.Remove(h.Node) && GodotObject.IsInstanceValid(h.Node))
        {
            WriteEmission(h.Node, EffectEmission.None);
        }
        _nodeBaseModulates.Remove(h.Node);
        _handles.Remove(h.Node);
    }
    private void OnTintChanged(Node node, Color color)
    {
        if (_nodeBaseModulates.ContainsKey(node))
        {
            _nodeBaseModulates[node] = color;
        }
    }

    private Dictionary<Node, VisualNodeHandle?> CollectCurrentNodes()
    {
        var nodes = new Dictionary<Node, VisualNodeHandle?>();
        if (Composer != null)
        {
            foreach (var h in Composer.GetVisualNodes(VisualQuery.All))
            {
                if (GodotObject.IsInstanceValid(h.Node)) { nodes[h.Node] = h; }
            }
        }
        if (Root != null && GodotObject.IsInstanceValid(Root))
        {
            var rootSprites = new List<Node>();
            VisualNodeAggregator.CollectSprites(Root, rootSprites);
            foreach (var n in rootSprites) { nodes.TryAdd(n, null); }
        }
        return nodes;
    }

    private void ApplyEffects()
    {
        if (_nodeBaseModulates.Count == 0 && _channelNodes.Count == 0) { return; }

        foreach (var (node, baseColor) in _nodeBaseModulates)
        {
            if (!GodotObject.IsInstanceValid(node)) { continue; }
            SetModulate(node, baseColor * Compose(node).Modulate);
        }

        _emissionWritesScratch.Clear();
        foreach (var (node, written) in _channelNodes)
        {
            if (!GodotObject.IsInstanceValid(node)) { continue; }
            var emission = Compose(node).Emission;
            if (emission != written) { _emissionWritesScratch.Add((node, emission)); }
        }
        foreach (var (node, emission) in _emissionWritesScratch)
        {
            WriteEmission(node, emission);
            _channelNodes[node] = emission;
        }
    }

    /// <summary>The modulate and emission the active effects that reach <paramref name="node"/> compose to.</summary>
    private (Color Modulate, Color Emission) Compose(Node node)
    {
        var handle = _handles.GetValueOrDefault(node);
        Color mixProduct = Colors.White;
        _mixEmissionsScratch.Clear();
        ActiveEffectHandle? bestOverride = null;
        foreach (var h in _activeEffects.Values)
        {
            if (!Reaches(h.Effect, handle)) { continue; }

            if (h.Effect.BlendMode == VisualEffectBlendMode.Override)
            {
                if (bestOverride == null
                    || h.Effect.Priority > bestOverride.Effect.Priority
                    || (h.Effect.Priority == bestOverride.Effect.Priority && h.PlayOrder > bestOverride.PlayOrder))
                {
                    bestOverride = h;
                }
            }
            else if (h.Effect.BlendMode == VisualEffectBlendMode.Mix)
            {
                mixProduct *= h.State.Modulate;
                _mixEmissionsScratch.Add(h.State.Emission);
            }
        }

        if (bestOverride != null) { return (bestOverride.State.Modulate, bestOverride.State.Emission); }
        return (mixProduct, EffectEmission.Combine(CollectionsMarshal.AsSpan(_mixEmissionsScratch)));
    }

    private static bool Reaches(VisualEffect effect, VisualNodeHandle? handle)
        => effect.Scope == null || (handle != null && effect.Scope.Matches(handle));

    private void ResetVisuals()
    {
        // Restore the LAYERED color (base × persistent tints), not just the bare
        // base color we cached at NodeAdded time. The previous bare-base reset
        // clobbered persistent tints registered with VisualEffectService — e.g.
        // a player color tint would vanish the moment any transient effect (hit
        // flash, sabotage tint) finished and ResetVisuals fired.
        var service = Composer?.Effects;
        foreach (var (node, baseColor) in _nodeBaseModulates)
        {
            if (!GodotObject.IsInstanceValid(node)) { continue; }
            var color = service != null ? service.ComputeEffectiveColorForNode(node) : baseColor;
            SetModulate(node, color);
        }
        foreach (var node in _channelNodes.Keys.ToList())
        {
            if (GodotObject.IsInstanceValid(node)) { WriteEmission(node, EffectEmission.None); }
            _channelNodes[node] = EffectEmission.None;
        }
    }

    private static void WriteEmission(Node node, Color emission)
    {
        switch (node)
        {
            case GeometryInstance3D geometry: geometry.SetInstanceShaderParameter(EffectEmission.Uniform, emission); break;
            case CanvasItem canvas: canvas.SetInstanceShaderParameter(EffectEmission.Uniform, emission); break;
        }
    }

    private void OnEffectFinished(VisualEffect effect) => StopEffect(effect);

    private static void SetModulate(Node node, Color color)
    {
        switch (node)
        {
            case SpriteBase3D s3d: s3d.Modulate = color; break;
            case CanvasItem ci: ci.Modulate = color; break;
        }
    }

    private static Color GetModulate(Node node)
    {
        return node switch
        {
            SpriteBase3D s3d => s3d.Modulate,
            CanvasItem ci => ci.Modulate,
            _ => Colors.White,
        };
    }

    #endregion

    private class ActiveEffectHandle
    {
        public VisualEffect Effect { get; set; } = null!;
        public IEffectApplier Applier { get; set; } = null!;
        public VisualEffectHandle State { get; set; } = null!;

        /// <summary>Rises with every play, so the later of two equal-priority Overrides wins even within one millisecond.</summary>
        public ulong PlayOrder { get; set; }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (Composer == null && Root == null)
        {
            warnings.Add("Set either Composer (for entities) or Root (for single-sprite props).");
        }
        return warnings.Concat(base._GetConfigurationWarnings() ?? []).ToArray();
    }

    public Node GetUnderlyingNode() => this;
}
