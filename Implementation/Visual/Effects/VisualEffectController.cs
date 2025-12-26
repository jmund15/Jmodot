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
    /// </summary>
    private readonly Dictionary<Node, Color> _nodeBaseModulates = new();

    public bool IsInitialized { get; private set; }
    public event Action Initialized;

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
        InitializeProviders();
        RefreshVisualNodes();

        IsInitialized = true;
        Initialized?.Invoke();
        return true;
    }

    public void OnPostInitialize() { }

    public override void _ExitTree()
    {
        base._ExitTree();
        StopAllEffects();
        UnsubscribeFromProviders();
    }

    public override void _Process(double delta)
    {
        ApplyEffects();
    }

    #region Public API

    /// <summary>
    /// Start playing a visual effect.
    /// </summary>
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

        // Create the state handle (the object that gets tweened)
        var stateHandle = new VisualEffectHandle();

        // Create the tween
        var tween = GetTree().CreateTween();

        // Configure the tween targeting the state handle
        effect.ConfigureTween(tween, stateHandle);

        tween.Play();
        tween.Finished += () => OnEffectFinished(effect);

        // Store handle
        var handle = new ActiveEffectHandle
        {
            Effect = effect,
            Tween = tween,
            State = stateHandle,
            StartTime = Time.GetTicksMsec()
        };

        _activeEffects[effect] = handle;

        // Enable processing
        SetProcess(true);

        // Immediate update to prevent 1-frame lag
        ApplyEffects();

        JmoLogger.Info(this, $"Started effect '{effect.ResourceName}'");
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

        handle.Tween?.Kill();
        handle.State.Free(); // Clean up the Godot Object
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

        JmoLogger.Info(this, $"Stopped effect '{effect.ResourceName}'");
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
        if (_nodeBaseModulates.Count == 0) return;

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
        _providers.Clear();
    }

    /// <summary>
    ///
    /// </summary>
    public void RefreshVisualNodes()
    {
        // Capture new list of nodes
        var currentNodes = GetAllVisualNodes();

        // Remove nodes that are no longer present
        // (Optional optimization: only remove if not in currentNodes)
        // For safety/simplicity, we can just rebuild the map or merge.
        // Let's merge: preserve existing base colors for existing nodes, capture for new ones.

        // Clean invalid nodes
        var invalidNodes = _nodeBaseModulates.Keys.Where(n => !GodotObject.IsInstanceValid(n)).ToList();
        foreach (var n in invalidNodes) _nodeBaseModulates.Remove(n);

        foreach (var node in currentNodes)
        {
            if (!_nodeBaseModulates.ContainsKey(node))
            {
                // New node found, capture its CURRENT state as base
                // NOTE: If we are mid-effect, this might capture the effect color as base if we aren't careful.
                // ideally RefreshVisualNodes is called when no effects are running, OR we calculate the inverse.
                // For now, assuming base modulate doesn't change implicitly.
                _nodeBaseModulates[node] = GetModulate(node);
            }
        }
    }

    private List<Node> GetAllVisualNodes()
    {
        var nodes = new List<Node>();

        foreach (var source in VisualSources)
        {
            if (source == null || !GodotObject.IsInstanceValid(source)) continue;

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
                if (source is SpriteBase3D) nodes.Add(source);
                nodes.AddRange(source.GetChildrenOfType<SpriteBase3D>());
                // Add 2D support?
                if (source is Node2D) nodes.AddRange(source.GetChildrenOfType<Node2D>().Where(n => n.GetType().Name.Contains("Sprite")));
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
        if (node is SpriteBase3D s3d) return s3d.Modulate;
        if (node is CanvasItem ci) return ci.Modulate;
        return Colors.White;
    }

    #endregion

    #region Internal Types

    private class ActiveEffectHandle
    {
        public VisualEffect Effect { get; set; } = null!;
        public Tween? Tween { get; set; }
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
