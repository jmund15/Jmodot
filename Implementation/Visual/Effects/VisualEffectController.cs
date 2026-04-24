namespace Jmodot.Implementation.Visual.Effects;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.Visual.Effects;
using Jmodot.Core.Components;
using Godot;
using GCol = Godot.Collections;
using Shared;
using Implementation.AI.BB;
using Implementation.Visual.Animation.Sprite;

/// <summary>
/// Central controller for applying visual effects across all active sprites in an entity.
/// Manages Tween-based effects using a Virtual Modulate architecture to support blending and overrides.
/// </summary>
[GlobalClass, Tool]
public partial class VisualEffectController : Node, IComponent
{
    /// <summary>
    /// Sources of visual nodes. Can be:
    /// - IVisualSpriteProvider (VisualComposer, AnimationVisibilityCoordinator)
    /// - SpriteBase3D / Sprite2D (single sprite)
    /// - Any Node3D (will search for sprite children)
    /// </summary>
    [Export] public GCol.Array<Node> VisualSources { get; set; } = new();

    /// <summary>
    /// Tracks active effects and their associated data.
    /// </summary>
    private readonly Dictionary<VisualEffect, ActiveEffectHandle> _activeEffects = new();

    /// <summary>
    /// Cached providers for subscribing to changes.
    /// </summary>
    private readonly List<IVisualSpriteProvider> _providers = new();

    /// <summary>
    /// Maps each visual node to its base modulate color (captured when tracking starts).
    /// When BaseModulationTracker is set, this dictionary is populated from the tracker.
    /// </summary>
    private readonly Dictionary<Node, Color> _nodeBaseModulates = new();

    /// <summary>
    /// Optional tracker for explicit base color management.
    /// When set, base colors are queried from the tracker instead of captured from sprite state.
    /// This is critical for equipment systems where base colors change at runtime.
    /// </summary>
    private BaseModulationTracker? _baseColorTracker;

    /// <summary>
    /// Optional subscription to a composer's effect-service TintChanged event. Stored
    /// so we can unsubscribe cleanly on _ExitTree.
    /// </summary>
    private IVisualEffectService? _subscribedService;

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    /// <summary>
    /// Set the base color tracker for this controller.
    /// When set, base colors are queried from the tracker instead of captured from sprite state.
    /// </summary>
    public void SetBaseColorTracker(BaseModulationTracker tracker)
    {
        _baseColorTracker = tracker;
        // Refresh to pick up tracked colors
        RefreshVisualNodes();
    }

    public override void _Ready()
    {
        base._Ready();

        // In editor, we might want to initialize immediately for tool usage,
        // but at runtime, we wait for Initialize loop.
        if (Engine.IsEditorHint())
        {
            InitializeProviders();
            RefreshVisualNodes();
        }

        SetProcess(false); // Only run process when effects are active
    }

    public bool Initialize(IBlackboard bb)
    {
        // Auto-wire with VisualComposer's BaseColorTracker if available in Blackboard.
        // Also subscribe to the composer's effect-service TintChanged event so
        // IVisualEffectService.SetBaseTint writes trigger a controller refresh.
        if (bb.TryGet(BBDataSig.VisualComposer, out VisualComposer composer))
        {
            if (_baseColorTracker == null)
            {
                SetBaseColorTracker(composer.BaseColorTracker);
            }

            _subscribedService = composer.Effects;
            _subscribedService.TintChanged += RefreshVisualNodes;
        }

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
    /// modulate for blending. Behavior is identical to the pre-4.4 inline setup
    /// since all existing effects produce a <c>ModulateTweenApplier</c> that
    /// replays the same <see cref="VisualEffect.ConfigureTween"/> call.
    /// </remarks>
    public void PlayEffect(VisualEffect effect)
    {
        if (effect == null)
        {
            JmoLogger.Warning(this, "Attempted to play null effect");
            return;
        }

        // Stop existing instance of this effect to restart it
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

    /// <summary>
    /// Stop a specific effect.
    /// </summary>
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
            // Reset all nodes to base color
            ResetVisuals();
            SetProcess(false);
        }
        else
        {
            // Re-evaluate remaining effects
            ApplyEffects();
        }
    }

    /// <summary>
    /// Stop all active effects.
    /// </summary>
    public void StopAllEffects()
    {
        // Copy keys to avoid modification exception
        var effects = _activeEffects.Keys.ToList();
        foreach (var effect in effects)
        {
            StopEffect(effect);
        }
    }

    /// <summary>
    /// Check if a specific effect is currently playing.
    /// </summary>
    public bool IsEffectPlaying(VisualEffect effect)
    {
        return effect != null && _activeEffects.ContainsKey(effect);
    }

    #endregion

    #region Core Logic

    private void ApplyEffects()
    {
        if (_nodeBaseModulates.Count == 0) { return; }

        // 1. Calculate the composite effect color
        Color finalEffectColor = Colors.White;

        // Check for dominant override (Highest Priority, then Newest)
        var overrideHandle = _activeEffects.Values
            .Where(h => h.Effect.BlendMode == VisualEffectBlendMode.Override)
            .OrderByDescending(h => h.Effect.Priority)
            .ThenByDescending(h => h.StartTime)
            .FirstOrDefault();

        if (overrideHandle != null)
        {
            // Override takes complete control
            finalEffectColor = overrideHandle.State.Modulate;
        }
        else
        {
            // Mix mode: Multiply all active mix effects
            foreach (var handle in _activeEffects.Values)
            {
                if (handle.Effect.BlendMode == VisualEffectBlendMode.Mix)
                {
                    finalEffectColor *= handle.State.Modulate;
                }
            }
        }

        //GD.Print($"Managed Nodes: {_nodeBaseModulates.Count}");
        //GD.Print($"Final Effect Color: {finalEffectColor}");

        // 2. Apply to all tracked nodes
        // We iterate backwards to safely handle invalid nodes
        foreach (var (node, baseColor) in _nodeBaseModulates)
        {
            if (!node.IsValid())
            {
                continue;
            }
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
        foreach (var source in VisualSources)
        {
            if (source is IVisualSpriteProvider provider)
            {
                _providers.Add(provider);
                provider.VisibleNodesChanged += RefreshVisualNodes; // Future proofing
            }
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
    /// Refreshes the list of tracked visual nodes and their base colors.
    /// When a BaseModulationTracker is set, base colors are queried from the tracker.
    /// Otherwise, colors are captured from the current sprite state.
    /// </summary>
    public void RefreshVisualNodes()
    {
        // Capture new list of nodes
        var currentNodes = GetAllVisualNodes();

        // Clean invalid nodes
        var invalidNodes = _nodeBaseModulates.Keys.Where(n => !GodotObject.IsInstanceValid(n)).ToList();
        foreach (var n in invalidNodes) { _nodeBaseModulates.Remove(n); }

        foreach (var node in currentNodes)
        {
            // Always update from tracker if available (tracker is source of truth)
            if (_baseColorTracker != null)
            {
                // Query tracker for explicit base color
                // If not registered, use White (no modification)
                _nodeBaseModulates[node] = _baseColorTracker.GetBaseColor(node);
            }
            else if (!_nodeBaseModulates.ContainsKey(node))
            {
                // No tracker - legacy behavior: capture current state as base
                // NOTE: This can capture effect colors if mid-effect. Use tracker to avoid.
                _nodeBaseModulates[node] = GetModulate(node);
            }
        }
    }

    private List<Node> GetAllVisualNodes()
    {
        var nodes = new List<Node>();

        foreach (var source in VisualSources)
        {
            if (source == null || !GodotObject.IsInstanceValid(source)) { continue; }

            if (source is IVisualSpriteProvider provider)
            {
                var providerNodes = provider.GetAllVisualNodes();
                if (providerNodes != null)
                {
                    nodes.AddRange(providerNodes);
                }
            }
            else
            {
                // Fallback: recursive sprite scan. Delegates to the shared aggregator
                // so there is exactly one implementation of this recursion in the codebase.
                VisualNodeAggregator.CollectSprites(source, nodes);
            }
        }
        return nodes;
    }

    #endregion

    #region Helpers

    private void OnEffectFinished(VisualEffect effect)
    {
        // Don't kill tween here, it's already finished. Just remove logic.
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
        else if (node is Node3D n3d)
        {
             // Fallback for some Node3D types?
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
        if (VisualSources == null || VisualSources.Count == 0)
        {
            warnings.Add("No visual sources configured.");
        }
        return warnings.ToArray();
    }

    public Node GetUnderlyingNode() => this;
}
