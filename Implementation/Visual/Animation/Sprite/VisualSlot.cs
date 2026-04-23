namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Core.Visual.Animation.Sprite;
using Godot;
using Shared;
using System;
using System.Collections.Generic;
using Core.Visual.Effects;
using Effects;

/// <summary>
/// Runtime slot — a first-class <see cref="IVisualSpriteProvider"/> that wraps one equipped item.
/// Handles instantiation, overrides, animator registration, and exposes the slot's visual/animation surface.
/// </summary>
public class VisualSlot : IVisualSpriteProvider
{
    public VisualSlotConfig Config { get; private set; }
    public VisualItemData? CurrentItem { get; private set; }

    /// <summary>
    /// The <see cref="IAnimComponent"/> on the currently equipped prefab, or null when empty.
    /// Exposed regardless of whether the slot is animation-independent — independent slots'
    /// animators are otherwise unreachable without string-searching the scene tree.
    /// </summary>
    public IAnimComponent? Animator { get; private set; }

    private CompositeAnimatorComponent _composite;
    private Node _slotRoot;
    private Node? _currentInstance;

    /// <summary>
    /// Optional tracker for base modulation colors.
    /// When set, base colors are registered here for VisualEffectController to query.
    /// </summary>
    private BaseModulationTracker? _baseColorTracker;

    // Visual Effect Tracking
    private readonly List<Node> _currentVisualNodes = new();
    private readonly List<Node> _currentVisibleNodes = new();
    private IVisualSpriteProvider? _prefabProvider;
    public event Action VisualNodesChanged = delegate { };
    public event Action VisibleNodesChanged = delegate { };

    public VisualSlot(VisualSlotConfig config, CompositeAnimatorComponent composite, Node slotRoot, BaseModulationTracker? baseColorTracker = null)
    {
        Config = config;
        _composite = composite;
        _slotRoot = slotRoot;
        _baseColorTracker = baseColorTracker;
    }

    public void Equip(VisualItemData? item)
    {
        if (CurrentItem == item)
        {
            return;
        }

        // Tear down any existing instance — mechanism only, no mandatory-slot policy,
        // no event firing. Equip owns the atomic transition and fires events once below.
        ClearInstance();

        if (item == null)
        {
            // Equip(null) on an optional slot is equivalent to Unequip. Fire once —
            // but only if we actually cleared something (otherwise same-state no-op).
            // ClearInstance already short-circuited on _currentInstance == null, so
            // the check here is: did we previously have an instance?
            if (_currentVisualNodes.Count == 0 && _currentVisibleNodes.Count == 0)
            {
                // Still-clean slate after ClearInstance — nothing to broadcast.
                // This path runs when Equip(null) is called on an empty slot.
                // Exit without events.
            }
            return;
        }

        CurrentItem = item;

        if (item.Prefab != null)
        {
            _currentInstance = item.Prefab.Instantiate();

            // AddChild synchronously so the node is tree-resident this frame for
            // systems that play animations immediately after equipping.
            // WARNING: Callers must defer Equip when called during _Ready or a
            // physics callback, or Godot throws "Tree Locked" / "Parent Busy".
            _slotRoot.AddChild(_currentInstance);

            ApplyOverrides(_currentInstance, item);

            // Resolve the animator. Expose it via `Animator` regardless of whether
            // we register it with the composite — independent slots still need
            // reachable animators for HSM consumers.
            Animator = GetAnimComponent(_currentInstance);
            if (Animator != null && !Config.IsAnimationIndependent)
            {
                _composite?.RegisterAnimator(Animator, isMaster: Config.IsTimeSource);
            }

            DetectVisualNodes(_currentInstance);
        }
        else
        {
            JmoLogger.Error(this, $"VisualSlot: Item '{item.Id}' has no Prefab assigned.");
        }

        // Atomic: one pair of events per Equip transition, regardless of whether
        // this was a fresh equip or a replace. Subscribers see the final state,
        // never intermediate empty states. Fixes the pre-Phase-3 multi-fire that
        // forced Wizard.OnVisualNodesChanged to use CallDeferred for coalescing.
        VisualNodesChanged?.Invoke();
        VisibleNodesChanged?.Invoke();
    }

    /// <summary>
    /// Policy-aware unequip for outside callers.
    /// - Optional slots: clear the instance and fire one pair of events.
    /// - Mandatory slots: revert to the configured default (delegating to Equip, which
    ///   fires one pair of events); or no-op with no events if already at default.
    /// Internal callers replacing an item should use Equip directly — it handles teardown.
    /// </summary>
    public void Unequip()
    {
        if (!Config.IsOptional)
        {
            if (Config.DefaultItem != null && CurrentItem != Config.DefaultItem)
            {
                Equip(Config.DefaultItem);
                return;
            }
            return; // Mandatory slot, already at default (or no default) — refuse to clear.
        }

        // Optional slot. If already empty, nothing to do — no events.
        if (_currentInstance == null)
        {
            return;
        }

        ClearInstance();
        CurrentItem = null;

        // Atomic: one pair of events for the transition from "something" to "nothing".
        VisualNodesChanged?.Invoke();
        VisibleNodesChanged?.Invoke();
    }

    /// <summary>
    /// Tears down the current instance unconditionally — pure mechanism. No mandatory
    /// policy, no event firing. Events are the orchestrators' responsibility (Equip /
    /// Unequip), which compose ClearInstance with a populate step when swapping.
    /// </summary>
    internal void ClearInstance()
    {
        if (_currentInstance == null)
        {
            return;
        }

        if (_baseColorTracker != null)
        {
            foreach (var visualNode in _currentVisualNodes)
            {
                _baseColorTracker.UnregisterSprite(visualNode);
            }
        }

        if (_prefabProvider != null)
        {
            _prefabProvider.VisibleNodesChanged -= OnPrefabVisibleNodesChanged;
            _prefabProvider.VisualNodesChanged -= OnPrefabVisualNodesChanged;
            _prefabProvider = null;
        }
        _currentVisualNodes.Clear();
        _currentVisibleNodes.Clear();

        if (Animator != null && !Config.IsAnimationIndependent)
        {
            _composite?.UnregisterAnimator(Animator);
        }
        Animator = null;

        _currentInstance.QueueFree();
        _currentInstance = null;
        CurrentItem = null;
    }

    private void ApplyOverrides(Node instance, VisualItemData item)
    {
        // Find the first sprite target. 2D takes priority over 3D (root or descendant)
        // to preserve historical behavior; mixed 2D+3D prefabs are an edge case.
        Node? sprite = null;
        if (instance is Sprite2D s2dRoot) { sprite = s2dRoot; }
        else if (instance.TryGetFirstChildOfType<Sprite2D>(out var s2dChild)) { sprite = s2dChild; }
        else if (instance is Sprite3D s3dRoot) { sprite = s3dRoot; }
        else if (instance.TryGetFirstChildOfType<Sprite3D>(out var s3dChild)) { sprite = s3dChild; }

        if (sprite == null) { return; }

        // Texture swap
        if (item.TextureOverride != null)
        {
            SetSpriteTexture(sprite, item.TextureOverride);
        }

        // Sprite sheet row selection — preserve X so animations don't reset, set Y
        if (item.SpriteSheetRowOverride >= 0)
        {
            SetSpriteRow(sprite, item.SpriteSheetRowOverride);
        }

        // Tint — apply Modulate if non-white, always register as base color
        // (white is still registered so the tracker knows this sprite exists).
        if (item.ModulateOverride != Colors.White)
        {
            SetSpriteModulate(sprite, item.ModulateOverride);
        }
        _baseColorTracker?.RegisterBaseColor(sprite, item.ModulateOverride);
    }

    private static void SetSpriteTexture(Node sprite, Texture2D texture)
    {
        switch (sprite)
        {
            case Sprite2D s2d: s2d.Texture = texture; break;
            case Sprite3D s3d: s3d.Texture = texture; break;
        }
    }

    private static void SetSpriteRow(Node sprite, int row)
    {
        switch (sprite)
        {
            case Sprite2D s2d: s2d.FrameCoords = new Vector2I(s2d.FrameCoords.X, row); break;
            case Sprite3D s3d: s3d.FrameCoords = new Vector2I(s3d.FrameCoords.X, row); break;
        }
    }

    private static void SetSpriteModulate(Node sprite, Color color)
    {
        switch (sprite)
        {
            case Sprite2D s2d: s2d.Modulate = color; break;
            case Sprite3D s3d: s3d.Modulate = color; break;
        }
    }

    private IAnimComponent GetAnimComponent(Node node)
    {
        // 1. Check if the node itself is an IAnimComponent
        if (node is IAnimComponent anim) { return anim; }

        // 2. Use NodeExts to find the first child implementing the interface
        if (node.TryGetFirstChildOfInterface<IAnimComponent>(out var childAnim))
        {
            return childAnim;
        }

        return null;
    }

    #region Visual Effect Support

    // IVisualSpriteProvider implementation. Invariant: GetVisibleNodes ⊆ GetAllVisualNodes.
    public IReadOnlyList<Node> GetAllVisualNodes() => _currentVisualNodes;
    public IReadOnlyList<Node> GetVisibleNodes() => _currentVisibleNodes;

    private void DetectVisualNodes(Node prefabRoot)
    {
        _currentVisualNodes.Clear();
        _currentVisibleNodes.Clear();

        // 1. Check if the prefab has a Coordinator (IVisualSpriteProvider)
        // This handles dynamic visibility changes (e.g. running animations)
        if (prefabRoot is IVisualSpriteProvider provider)
        {
            _prefabProvider = provider;
        }
        else if (prefabRoot.TryGetFirstChildOfInterface<IVisualSpriteProvider>(out provider))
        {
            _prefabProvider = provider;
        }

        if (_prefabProvider != null)
        {
            _prefabProvider.VisualNodesChanged += OnPrefabVisualNodesChanged;
            _prefabProvider.VisibleNodesChanged += OnPrefabVisibleNodesChanged;
            _currentVisualNodes.AddRange(_prefabProvider.GetAllVisualNodes());
            _currentVisibleNodes.AddRange(_prefabProvider.GetVisibleNodes());
        }
        else
        {
            // 2. Fallback: Recursively find all static sprites.
            // Visible and visual are conflated in this branch — static prefabs have no
            // per-animation visibility model. AnimationVisibilityCoordinator is the only
            // provider that distinguishes them today.
            VisualNodeAggregator.CollectSprites(prefabRoot, _currentVisualNodes);
            VisualNodeAggregator.CollectSprites(prefabRoot, _currentVisibleNodes);
        }

        // No event firing here — populate is mechanism-only. Equip owns the atomic fire.
    }

    private void OnPrefabVisualNodesChanged()
    {
        // shouldn't be possible, but in case of unequipping race condition?
        if (_prefabProvider != null)
        {
            _currentVisualNodes.Clear();
            _currentVisualNodes.AddRange(_prefabProvider.GetAllVisualNodes());

            VisualNodesChanged?.Invoke();
        }
    }

    private void OnPrefabVisibleNodesChanged()
    {
        // shouldn't be possible, but in case of unequipping race condition?
        if (_prefabProvider != null)
        {
            _currentVisibleNodes.Clear();
            _currentVisibleNodes.AddRange(_prefabProvider.GetVisibleNodes());

            VisibleNodesChanged?.Invoke();
        }
    }

    #endregion
}
