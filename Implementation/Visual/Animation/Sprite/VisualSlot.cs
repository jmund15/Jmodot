namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Core.Visual.Animation.Sprite;
using Godot;
using Shared;
using System;
using System.Collections.Generic;
using Core.Visual.Effects;
using Effects;

/// <summary>
/// Helper class representing a runtime slot.
/// Handles instantiation, overrides, and registration.
/// </summary>
public class VisualSlot // TODO: make IVisualSpriteProvider?
{
    public VisualSlotConfig Config { get; private set; }
    public VisualItemData? CurrentItem { get; private set; }

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

        // 1. Cleanup existing
        // If we are providing a new item (item != null), we force the unequip.
        // This prevents Unequip() from triggering its "Mandatory Slot" logic
        // which recursively calls Equip() again.
        bool isReplacing = (item != null);
        Unequip(force: isReplacing);

        if (item == null)
        {
            return; // Unequip complete
        }

        CurrentItem = item;

        if (item.Prefab != null)
        {
            // 2. Instantiate
            _currentInstance = item.Prefab.Instantiate();

            // We use AddChild immediately so that the node is available in the tree THIS frame.
            // This is critical for systems that want to play an animation immediately after equipping (e.g. VisualSlotCompoundState).
            // WARNING: If Equip() is called during a Physics Callback (e.g. Area3D.BodyEntered) OR during Initialization (Parent _Ready),
            // this will throw a "Tree Locked" or "Parent Busy" error.
            // The caller must ensure they are deferring the Equip call if they are in a dangerous context.
            _slotRoot.AddChild(_currentInstance);

            // 3. Apply Overrides (Texture, Row, Tint)
            ApplyOverrides(_currentInstance, item);

            // 4. Register with Composite (unless slot is animation-independent)
            // We look for an IAnimComponent on the root or children
            var anim = GetAnimComponent(_currentInstance);
            if (anim != null && !Config.IsAnimationIndependent)
            {
                _composite?.RegisterAnimator(anim, isMaster: Config.IsTimeSource);
            }

            // 5. Track Visual Nodes for Effects
            DetectVisualNodes(_currentInstance);
        }
        else
        {
            JmoLogger.Error(this, $"VisualSlot: Item '{item.Id}' has no Prefab assigned.");
        }
    }

    public void Unequip(bool force = false)
    {
        // Prevent unequipping mandatory slots unless forced (e.g. during a swap)
        if (!Config.IsOptional && !force)
        {
            // If we have a default, revert to it
            if (Config.DefaultItem != null && CurrentItem != Config.DefaultItem)
            {
                Equip(Config.DefaultItem);
                return;
            }
            return; // Cannot be empty
        }

        if (_currentInstance != null)
        {
            // Cleanup visual tracking - unregister from base color tracker
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
                _prefabProvider = null;
            }
            _currentVisualNodes.Clear();
            _currentVisibleNodes.Clear();
            VisualNodesChanged?.Invoke();
            VisibleNodesChanged?.Invoke();

            var anim = GetAnimComponent(_currentInstance);
            if (anim != null && !Config.IsAnimationIndependent)
            {
                _composite?.UnregisterAnimator(anim);
            }

            _currentInstance.QueueFree();
            _currentInstance = null;
        }

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

    public IReadOnlyList<Node> GetCurrentVisualNodes() => _currentVisualNodes;
    public IReadOnlyList<Node> GetCurrentVisibleNodes() => _currentVisibleNodes;

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

        //GD.Print($"Just finished equipping Visual Slot! visual config: {CurrentItem.Id}");

        VisualNodesChanged?.Invoke();
        VisibleNodesChanged?.Invoke();
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
