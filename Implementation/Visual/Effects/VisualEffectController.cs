namespace Jmodot.Implementation.Visual.Effects;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.Visual.Effects;
using Godot;
using Jmodot.Core.Components;
using Implementation.Visual.Animation.Sprite;
using Shared;

/// <summary>
/// Central controller for applying visual effects across the active sprite set
/// of an entity. Manages tween-driven effects via the Virtual Modulate
/// architecture (blend modes, priority-ordered override).
/// </summary>
/// <remarks>
/// Phase 5.1 — replaced the Blackboard-based tracker auto-wire with an explicit
/// <see cref="Composer"/> export. When set, the controller:
/// <list type="bullet">
/// <item>Queries <see cref="IVisualEffectService.GetBaseColor"/> for per-node
/// base colors during <see cref="RefreshVisualNodes"/>.</item>
/// <item>Subscribes to <see cref="IVisualEffectService.TintChanged"/> so
/// <c>SetBaseTint</c> writes trigger an automatic cache rebuild.</item>
/// </list>
/// Phase 5.2 — replaced <c>Array&lt;Node&gt; VisualSources</c> with a single
/// <see cref="Root"/> export. The same dual-path logic applies: if the root is
/// an <see cref="IVisualSpriteProvider"/>, its node set is used; otherwise a
/// recursive sprite scan finds every <c>SpriteBase3D</c> / <c>Sprite2D</c> under it.
/// </remarks>
[GlobalClass, Tool]
public partial class VisualEffectController : Node, IComponent
{
    /// <summary>
    /// Source of visual nodes for effect application. Accepts either an
    /// <see cref="IVisualSpriteProvider"/> (VisualComposer, AnimationVisibilityCoordinator)
    /// or any node under which a recursive sprite scan should be run (single-sprite props).
    /// </summary>
    [Export] public Node? Root { get; set; }

    /// <summary>
    /// Optional composer-level effect service. When set, the controller queries this
    /// for per-node base colors and subscribes to its TintChanged event to refresh
    /// its cache. When null, the controller falls back to capturing sprite state at
    /// first touch (legacy behavior for single-sprite props without a composer).
    /// </summary>
    [Export] public VisualComposer? Composer { get; set; }

    /// <summary>
    /// Tracks active effects and their associated data.
    /// </summary>
    private readonly Dictionary<VisualEffect, ActiveEffectHandle> _activeEffects = new();

    /// <summary>
    /// Cached providers for subscribing to changes.
    /// </summary>
    private readonly List<IVisualSpriteProvider> _providers = new();

    /// <summary>
    /// Maps each visual node to its base modulate color. When an <see cref="IVisualEffectService"/>
    /// is wired via <see cref="Composer"/>, base colors are queried from the service on refresh;
    /// otherwise colors are captured from the current sprite state on first touch.
    /// </summary>
    private readonly Dictionary<Node, Color> _nodeBaseModulates = new();

    /// <summary>
    /// The effect service we subscribed to TintChanged on. Tracked so we can
    /// unsubscribe cleanly on <see cref="_ExitTree"/>.
    /// </summary>
    private IVisualEffectService? _subscribedService;

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    public override void _Ready()
    {
        base._Ready();

        SetProcess(false); // Only run process when effects are active

        // Wire up to the composer's effect service, if present.
        if (Composer != null && !Engine.IsEditorHint())
        {
            _subscribedService = Composer.Effects;
            _subscribedService.TintChanged += RefreshVisualNodes;
        }

        if (Engine.IsEditorHint())
        {
            InitializeProviders();
            RefreshVisualNodes();
        }
    }

    /// <summary>
    /// Initialize from a blackboard. Retained for <see cref="IComponent"/> compliance
    /// but no longer does tracker auto-wire — that moved to the <see cref="Composer"/>
    /// export path in <see cref="_Ready"/>.
    /// </summary>
    public bool Initialize(IBlackboard bb)
    {
        InitializeProviders();
        RefreshVisualNodes();

        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }

    public override void _ExitTree()
    {
        base._ExitTree();
        StopAllEffects();
        UnsubscribeFromProviders();

        if (_subscribedService != null)
        {
            _subscribedService.TintChanged -= RefreshVisualNodes;
            _subscribedService = null;
        }
    }

    public override void _Process(double delta)
    {
        ApplyEffects();
    }

    #region Public API

    /// <summary>
    /// Start playing a visual effect.
    /// </summary>
    /// <remarks>
    /// Phase 4.4 — delegates tween setup to <see cref="VisualEffect.CreateApplier"/>.
    /// The applier owns its tween + handle; the controller just reads the handle's
    /// modulate for blending.
    /// </remarks>
    public void PlayEffect(VisualEffect effect)
    {
        if (effect == null)
        {
            JmoLogger.Warning(this, "Attempted to play null effect");
            return;
        }

        if (_activeEffects.ContainsKey(effect))
        {
            StopEffect(effect);
        }

        var applier = effect.CreateApplier();
        var stateHandle = applier.Begin(GetTree(), () => OnEffectFinished(effect));

        _activeEffects[effect] = new ActiveEffectHandle
        {
            Effect = effect,
            Applier = applier,
            State = stateHandle,
            StartTime = Time.GetTicksMsec()
        };

        SetProcess(true);
        ApplyEffects(); // Immediate update to prevent 1-frame lag
    }

    public void StopEffect(VisualEffect effect)
    {
        if (effect == null || !_activeEffects.TryGetValue(effect, out var handle))
        {
            return;
        }

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
        foreach (var effect in effects)
        {
            StopEffect(effect);
        }
    }

    public bool IsEffectPlaying(VisualEffect effect)
    {
        return effect != null && _activeEffects.ContainsKey(effect);
    }

    #endregion

    #region Core Logic

    private void ApplyEffects()
    {
        if (_nodeBaseModulates.Count == 0) { return; }

        Color finalEffectColor = Colors.White;

        // Override takes complete control — highest Priority, then newest.
        var overrideHandle = _activeEffects.Values
            .Where(h => h.Effect.BlendMode == VisualEffectBlendMode.Override)
            .OrderByDescending(h => h.Effect.Priority)
            .ThenByDescending(h => h.StartTime)
            .FirstOrDefault();

        if (overrideHandle != null)
        {
            finalEffectColor = overrideHandle.State.Modulate;
        }
        else
        {
            foreach (var handle in _activeEffects.Values)
            {
                if (handle.Effect.BlendMode == VisualEffectBlendMode.Mix)
                {
                    finalEffectColor *= handle.State.Modulate;
                }
            }
        }

        foreach (var (node, baseColor) in _nodeBaseModulates)
        {
            if (!node.IsValid()) { continue; }
            SetModulate(node, baseColor * finalEffectColor);
        }
    }

    private void ResetVisuals()
    {
        foreach (var (node, baseColor) in _nodeBaseModulates)
        {
            if (GodotObject.IsInstanceValid(node))
            {
                SetModulate(node, baseColor);
            }
        }
    }

    #endregion

    #region Provider & Node Management

    private void InitializeProviders()
    {
        if (Root is IVisualSpriteProvider provider)
        {
            _providers.Add(provider);
            provider.VisibleNodesChanged += RefreshVisualNodes;
        }
    }

    private void UnsubscribeFromProviders()
    {
        foreach (var provider in _providers)
        {
            provider.VisibleNodesChanged -= RefreshVisualNodes;
        }
        _providers.Clear();
    }

    /// <summary>
    /// Refreshes the list of tracked visual nodes and their base colors. When a
    /// composer service is wired, base colors are queried from the service;
    /// otherwise colors are captured from current sprite state on first touch.
    /// </summary>
    public void RefreshVisualNodes()
    {
        var currentNodes = GetAllVisualNodes();

        // Drop invalid nodes.
        var invalidNodes = _nodeBaseModulates.Keys.Where(n => !GodotObject.IsInstanceValid(n)).ToList();
        foreach (var n in invalidNodes) { _nodeBaseModulates.Remove(n); }

        var service = _subscribedService ?? Composer?.Effects;
        foreach (var node in currentNodes)
        {
            if (service != null)
            {
                // Service is source of truth — always update so base-tint changes
                // applied elsewhere propagate.
                _nodeBaseModulates[node] = service.GetBaseColor(node);
            }
            else if (!_nodeBaseModulates.ContainsKey(node))
            {
                // No service — legacy behavior: capture current state as base. May
                // inadvertently capture mid-effect color; wire a composer to avoid.
                _nodeBaseModulates[node] = GetModulate(node);
            }
        }
    }

    private List<Node> GetAllVisualNodes()
    {
        var nodes = new List<Node>();

        if (Root == null || !GodotObject.IsInstanceValid(Root)) { return nodes; }

        if (Root is IVisualSpriteProvider provider)
        {
            var providerNodes = provider.GetAllVisualNodes();
            if (providerNodes != null)
            {
                nodes.AddRange(providerNodes);
            }
        }
        else
        {
            // Fallback: recursive sprite scan via the shared aggregator.
            VisualNodeAggregator.CollectSprites(Root, nodes);
        }

        return nodes;
    }

    #endregion

    #region Helpers

    private void OnEffectFinished(VisualEffect effect)
    {
        StopEffect(effect);
    }

    private static void SetModulate(Node node, Color color)
    {
        if (node is SpriteBase3D s3d)
        {
            s3d.Modulate = color;
        }
        else if (node is CanvasItem ci)
        {
            ci.Modulate = color;
        }
    }

    private static Color GetModulate(Node node)
    {
        if (node is SpriteBase3D s3d) { return s3d.Modulate; }
        if (node is CanvasItem ci) { return ci.Modulate; }
        return Colors.White;
    }

    #endregion

    #region Internal Types

    private class ActiveEffectHandle
    {
        public VisualEffect Effect { get; set; } = null!;
        public IEffectApplier Applier { get; set; } = null!;
        public VisualEffectHandle State { get; set; } = null!;
        public ulong StartTime { get; set; }
    }

    #endregion

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (Root == null)
        {
            warnings.Add("No Root assigned — controller will track no visual nodes.");
        }
        return warnings.ToArray();
    }

    public Node GetUnderlyingNode() => this;
}
