namespace Jmodot.Implementation.Visual.Effects;

using System;
using System.Collections.Generic;
using System.Linq;
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
/// auto-wire of <c>BaseModulationTracker</c>; fixes the layering violation where
/// Implementation.Visual.Effects reached into Implementation.AI.BB.BBDataSig).
/// </para>
/// <para>
/// When a composer is set: subscribes to its <see cref="IVisualNodeProvider.NodeAdded"/>
/// / <see cref="IVisualNodeProvider.NodeRemoved"/> events for node tracking, and to
/// <see cref="IVisualEffectService.TintChanged"/> for base-color updates that follow
/// equipment swaps.
/// </para>
/// <para>
/// When only <see cref="Root"/> is set (single-sprite props with no composer):
/// uses <see cref="VisualNodeAggregator.CollectSprites"/> to find every sprite under
/// the root and captures their current Modulate as their base color. This path also
/// fixes the audit-branch-identified type-NAME string-match bug — the aggregator
/// uses pattern-based type checks, not name-substring checks.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class VisualEffectController : Node, IComponent
{
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

        if (Composer != null)
        {
            Composer.NodeAdded += OnNodeAdded;
            Composer.NodeRemoved += OnNodeRemoved;
            _subscribedService = Composer.Effects;
            if (_subscribedService != null)
            {
                _subscribedService.TintChanged += OnTintChanged;
            }
        }

        RefreshVisualNodes();
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
        OnPostInitialize();
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

        var stateHandle = new VisualEffectHandle();
        var tween = GetTree().CreateTween();
        effect.ConfigureTween(tween, stateHandle);

        tween.Play();
        tween.Finished += () => OnEffectFinished(effect);

        var handle = new ActiveEffectHandle
        {
            Effect = effect,
            Tween = tween,
            State = stateHandle,
            StartTime = Time.GetTicksMsec(),
        };
        _activeEffects[effect] = handle;

        SetProcess(true);
        ApplyEffects();
    }

    public void StopEffect(VisualEffect effect)
    {
        if (effect == null || !_activeEffects.TryGetValue(effect, out var handle)) { return; }

        handle.Tween?.Kill();
        if (GodotObject.IsInstanceValid(handle.State)) { handle.State.Free(); }
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
        var current = CollectCurrentNodes();

        // Drop nodes no longer present (or freed).
        var stale = _nodeBaseModulates.Keys.Where(n => !GodotObject.IsInstanceValid(n) || !current.Contains(n)).ToList();
        foreach (var n in stale) { _nodeBaseModulates.Remove(n); }

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

    private void OnNodeAdded(VisualNodeHandle h) => RefreshVisualNodes();
    private void OnNodeRemoved(VisualNodeHandle h)
    {
        _nodeBaseModulates.Remove(h.Node);
    }
    private void OnTintChanged(Node node, Color color)
    {
        if (_nodeBaseModulates.ContainsKey(node))
        {
            _nodeBaseModulates[node] = color;
        }
    }

    private HashSet<Node> CollectCurrentNodes()
    {
        var set = new HashSet<Node>();
        if (Composer != null)
        {
            foreach (var h in Composer.GetVisualNodes(VisualQuery.All))
            {
                if (GodotObject.IsInstanceValid(h.Node)) { set.Add(h.Node); }
            }
        }
        if (Root != null && GodotObject.IsInstanceValid(Root))
        {
            var rootSprites = new List<Node>();
            VisualNodeAggregator.CollectSprites(Root, rootSprites);
            foreach (var n in rootSprites) { set.Add(n); }
        }
        return set;
    }

    private void ApplyEffects()
    {
        if (_nodeBaseModulates.Count == 0) { return; }

        Color finalEffectColor = Colors.White;

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
            if (!GodotObject.IsInstanceValid(node)) { continue; }
            SetModulate(node, baseColor * finalEffectColor);
        }
    }

    private void ResetVisuals()
    {
        foreach (var (node, baseColor) in _nodeBaseModulates)
        {
            if (GodotObject.IsInstanceValid(node)) { SetModulate(node, baseColor); }
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
        public Tween? Tween { get; set; }
        public VisualEffectHandle State { get; set; } = null!;
        public ulong StartTime { get; set; }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (Composer == null && Root == null)
        {
            warnings.Add("Set either Composer (for entities) or Root (for single-sprite props).");
        }
        return warnings.ToArray();
    }

    public Node GetUnderlyingNode() => this;
}
