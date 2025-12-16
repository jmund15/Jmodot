namespace Jmodot.Implementation.Visual.Effects;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Visual.Effects;
using Godot;
using GCol = Godot.Collections;
using Shared;

/// <summary>
/// Central controller for applying visual effects across all active sprites in an entity.
/// Manages Tween-based effects, handles dynamic sprite changes mid-effect, and implements priority override.
/// 
/// Usage:
/// 1. Set VisualSources to point to IVisualSpriteProvider nodes (VisualComposer) or direct Sprite nodes
/// 2. Call PlayEffect(effect) to start an effect
/// 3. The controller handles aggregating sprites, creating Tweens, and responding to sprite changes
/// </summary>
[GlobalClass]
public partial class VisualEffectController : Node
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
    /// The currently dominant effect (highest priority, or most recent if tied).
    /// </summary>
    private VisualEffect? _currentDominantEffect;

    public override void _Ready()
    {
        base._Ready();
        InitializeProviders();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        StopAllEffects();
        UnsubscribeFromProviders();
    }

    #region Public API

    /// <summary>
    /// Start playing a visual effect on all active sprites.
    /// If an effect with higher or equal priority is already playing, this effect takes over.
    /// </summary>
    public void PlayEffect(VisualEffect effect)
    {
        if (effect == null)
        {
            JmoLogger.Warning(this, "Attempted to play null effect");
            return;
        }

        // Check priority override
        if (_currentDominantEffect != null && effect.Priority < _currentDominantEffect.Priority)
        {
            JmoLogger.Trace(this, $"Effect '{effect.ResourceName}' blocked by higher priority effect '{_currentDominantEffect.ResourceName}'");
            return;
        }

        // Stop current dominant effect if we're replacing it
        if (_currentDominantEffect != null)
        {
            StopEffect(_currentDominantEffect);
        }

        // Get all current visual nodes
        var nodes = GetAllActiveVisualNodes();
        if (nodes.Count == 0)
        {
            JmoLogger.Warning(this, "No active visual nodes found to apply effect");
            return;
        }

        // Create the effect handle
        var handle = new ActiveEffectHandle
        {
            Effect = effect,
            NodeStates = new Dictionary<Node, Dictionary<string, Variant>>(),
            StartTime = Time.GetTicksMsec()
        };

        // Capture state and apply effect to each node
        var tween = CreateTween();
        tween.SetParallel(true); // All nodes animate in parallel

        foreach (var node in nodes)
        {
            if (!VisualEffect.IsVisualNode(node)) continue;

            // Capture original state
            handle.NodeStates[node] = effect.CaptureState(node);

            // Configure tween for this node
            effect.ConfigureTween(tween, node);
        }

        tween.SetParallel(false);
        tween.Finished += () => OnEffectFinished(effect);

        handle.Tween = tween;
        _activeEffects[effect] = handle;
        _currentDominantEffect = effect;

        JmoLogger.Trace(this, $"Started effect '{effect.ResourceName}' on {nodes.Count} nodes");
    }

    /// <summary>
    /// Stop a specific effect, restoring all affected nodes to their original state.
    /// </summary>
    public void StopEffect(VisualEffect effect)
    {
        if (effect == null || !_activeEffects.TryGetValue(effect, out var handle))
        {
            return;
        }

        // Kill the tween
        handle.Tween?.Kill();

        // Restore all node states
        foreach (var (node, state) in handle.NodeStates)
        {
            if (GodotObject.IsInstanceValid(node))
            {
                effect.RestoreState(node, state);
            }
        }

        _activeEffects.Remove(effect);

        if (_currentDominantEffect == effect)
        {
            _currentDominantEffect = null;
        }

        JmoLogger.Trace(this, $"Stopped effect '{effect.ResourceName}'");
    }

    /// <summary>
    /// Stop all active effects and restore all nodes.
    /// </summary>
    public void StopAllEffects()
    {
        // Create a copy since we're modifying the collection
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

    #region Provider Management

    private void InitializeProviders()
    {
        foreach (var source in VisualSources)
        {
            if (source is IVisualSpriteProvider provider)
            {
                _providers.Add(provider);
                provider.VisualNodesChanged += OnVisualNodesChanged;
            }
        }
    }

    private void UnsubscribeFromProviders()
    {
        foreach (var provider in _providers)
        {
            provider.VisualNodesChanged -= OnVisualNodesChanged;
        }
        _providers.Clear();
    }

    private void OnVisualNodesChanged()
    {
        // If an effect is playing, we need to handle new/removed nodes
        if (_currentDominantEffect == null || !_activeEffects.TryGetValue(_currentDominantEffect, out var handle))
        {
            return;
        }

        var currentNodes = GetAllActiveVisualNodes();
        var trackedNodes = handle.NodeStates.Keys.ToHashSet();

        // Find new nodes (in current but not tracked)
        var newNodes = currentNodes.Where(n => !trackedNodes.Contains(n) && VisualEffect.IsVisualNode(n)).ToList();

        // Find removed nodes (tracked but not in current)
        var removedNodes = trackedNodes.Where(n => !currentNodes.Contains(n)).ToList();

        // Restore and remove tracking for removed nodes
        foreach (var node in removedNodes)
        {
            if (GodotObject.IsInstanceValid(node) && handle.NodeStates.TryGetValue(node, out var state))
            {
                _currentDominantEffect.RestoreState(node, state);
            }
            handle.NodeStates.Remove(node);
        }

        // Add new nodes to the running effect
        if (newNodes.Count > 0)
        {
            AddNodesToRunningEffect(handle, newNodes);
        }
    }

    private void AddNodesToRunningEffect(ActiveEffectHandle handle, List<Node> newNodes)
    {
        // For new nodes joining mid-effect, we start a new tween for just these nodes
        // This is simpler than trying to sync them to the existing tween's progress
        var tween = CreateTween();
        tween.SetParallel(true);

        foreach (var node in newNodes)
        {
            handle.NodeStates[node] = handle.Effect.CaptureState(node);
            handle.Effect.ConfigureTween(tween, node);
        }

        // Note: This tween runs independently and may finish at a different time
        // For most effects (flash, tint), this is acceptable behavior
        JmoLogger.Trace(this, $"Added {newNodes.Count} new nodes to running effect '{handle.Effect.ResourceName}'");
    }

    #endregion

    #region Node Aggregation

    /// <summary>
    /// Gather all active visual nodes from all sources.
    /// </summary>
    private List<Node> GetAllActiveVisualNodes()
    {
        var nodes = new List<Node>();

        foreach (var source in VisualSources)
        {
            if (source == null || !GodotObject.IsInstanceValid(source)) continue;

            if (source is IVisualSpriteProvider provider)
            {
                // Use the provider's method
                nodes.AddRange(provider.GetActiveVisualNodes());
            }
            else if (source is SpriteBase3D or Sprite2D)
            {
                // Direct sprite reference
                nodes.Add(source);
            }
            else if (source is Node3D node3D)
            {
                // Search for sprite children
                FindSpritesRecursive(node3D, nodes);
            }
            else if (source is CanvasItem canvasItem)
            {
                // Search for sprite children in 2D
                FindSpritesRecursive(canvasItem, nodes);
            }
        }

        return nodes;
    }

    private static void FindSpritesRecursive(Node parent, List<Node> results)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is SpriteBase3D or Sprite2D)
            {
                results.Add(child);
            }

            // Continue searching in children
            FindSpritesRecursive(child, results);
        }
    }

    #endregion

    #region Effect Lifecycle

    private void OnEffectFinished(VisualEffect effect)
    {
        if (!_activeEffects.TryGetValue(effect, out var handle))
        {
            return;
        }

        // Restore all states
        foreach (var (node, state) in handle.NodeStates)
        {
            if (GodotObject.IsInstanceValid(node))
            {
                effect.RestoreState(node, state);
            }
        }

        _activeEffects.Remove(effect);

        if (_currentDominantEffect == effect)
        {
            _currentDominantEffect = null;
        }

        JmoLogger.Trace(this, $"Effect '{effect.ResourceName}' finished naturally");
    }

    #endregion

    #region Internal Types

    /// <summary>
    /// Tracks the state of an active effect.
    /// </summary>
    private class ActiveEffectHandle
    {
        public VisualEffect Effect { get; set; } = null!;
        public Tween? Tween { get; set; }
        public Dictionary<Node, Dictionary<string, Variant>> NodeStates { get; set; } = new();
        public ulong StartTime { get; set; }
    }

    #endregion
}
