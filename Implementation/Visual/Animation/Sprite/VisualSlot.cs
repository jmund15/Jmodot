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

            // 4. Register with Composite
            // We look for an IAnimComponent on the root or children
            var anim = GetAnimComponent(_currentInstance);
            //GD.Print($"VisualSlot: Item '{item.Id}' found anim component: {anim}.");
            if (anim != null)
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

    private void InstantiateEquippedItem(VisualItemData item)
    {
        _slotRoot.AddChild(_currentInstance);
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
            if (anim != null)
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
        // Robustly find the sprite (2D or 3D)
        // Check 2D first
        var sprite2D = instance as Sprite2D;
        if (sprite2D != null || instance.TryGetFirstChildOfType<Sprite2D>(out sprite2D))
        {
            ApplyOverrides2D(sprite2D!, item);
            return;
        }

        // Check 3D (SpriteBase3D covers both Sprite3D and AnimatedSprite3D)
        var sprite3D = instance as Sprite3D;

        if (sprite3D != null || instance.TryGetFirstChildOfType<Sprite3D>(out sprite3D))
        {
            ApplyOverrides3D(sprite3D!, item);
        }
    }

    private void ApplyOverrides2D(Sprite2D sprite, VisualItemData item)
    {
        // Texture Swap
        if (item.TextureOverride != null)
        {
            sprite.Texture = item.TextureOverride;
        }

        // Sprite Sheet Row Selection
        // We only set the Y coordinate. We preserve X so animations don't reset.
        if (item.SpriteSheetRowOverride >= 0)
        {
            sprite.FrameCoords = new Vector2I(sprite.FrameCoords.X, item.SpriteSheetRowOverride);
        }

        // Tint - register with tracker for proper effect blending
        if (item.ModulateOverride != Colors.White)
        {
            sprite.Modulate = item.ModulateOverride;
            _baseColorTracker?.RegisterBaseColor(sprite, item.ModulateOverride);
        }
        else
        {
            // No override - still register white as the base color
            _baseColorTracker?.RegisterBaseColor(sprite, Colors.White);
        }
    }

    private void ApplyOverrides3D(Sprite3D sprite, VisualItemData item)
    {
        // Texture Swap
        if (item.TextureOverride != null)
        {
            sprite.Texture = item.TextureOverride;
        }

        // Sprite Sheet Row Selection
        if (item.SpriteSheetRowOverride >= 0)
        {
            sprite.FrameCoords = new Vector2I(sprite.FrameCoords.X, item.SpriteSheetRowOverride);
        }

        // Tint - register with tracker for proper effect blending
        if (item.ModulateOverride != Colors.White)
        {
            sprite.Modulate = item.ModulateOverride;
            _baseColorTracker?.RegisterBaseColor(sprite, item.ModulateOverride);
        }
        else
        {
            // No override - still register white as the base color
            _baseColorTracker?.RegisterBaseColor(sprite, Colors.White);
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
            // 2. Fallback: Recursively find all static sprites
            // Currently here visible and visual are treated the same. (TODO: track visiblity of individual sprites here?)
            FindSpritesRecursive(prefabRoot, _currentVisualNodes);
            FindSpritesRecursive(prefabRoot, _currentVisibleNodes);
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

    private static void FindSpritesRecursive(Node parent, List<Node> results)
    {
        if (parent is SpriteBase3D or Sprite2D)
        {
            results.Add(parent);
        }

        foreach (var child in parent.GetChildren())
        {
            FindSpritesRecursive(child, results);
        }
    }

    #endregion
}
