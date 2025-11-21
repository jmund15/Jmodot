namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Core.Visual.Animation.Sprite;

/// <summary>
/// Helper class representing a runtime slot.
/// Handles instantiation, overrides, and registration.
/// </summary>
public class VisualSlot
{
    public VisualSlotConfig Config { get; private set; }
    public VisualItemData CurrentItem { get; private set; }

    private CompositeAnimatorComponent _composite;
    private Node _slotRoot;
    private Node _currentInstance;

    public VisualSlot(VisualSlotConfig config, CompositeAnimatorComponent composite, Node slotRoot)
    {
        Config = config;
        _composite = composite;
        _slotRoot = slotRoot;
    }

    public void Equip(VisualItemData item)
    {
        if (CurrentItem == item) return;

        // 1. Cleanup existing
        Unequip(force: false);

        if (item == null) return; // Unequip complete

        CurrentItem = item;

        if (item.Prefab != null)
        {
            // 2. Instantiate
            _currentInstance = item.Prefab.Instantiate();
            _slotRoot.AddChild(_currentInstance);

            // 3. Apply Overrides (Texture, Row, Tint)
            ApplyOverrides(_currentInstance, item);

            // 4. Register with Composite
            // We look for an IAnimComponent on the root or children
            var anim = GetAnimComponent(_currentInstance);
            if (anim != null)
            {
                _composite?.RegisterAnimator(anim, isMaster: Config.IsTimeSource);
            }
        }
        else
        {
            GD.PrintErr($"VisualSlot: Item '{item.Id}' has no Prefab assigned.");
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
            var anim = GetAnimComponent(_currentInstance);
            if (anim != null) _composite?.UnregisterAnimator(anim);

            _currentInstance.QueueFree();
            _currentInstance = null;
        }

        CurrentItem = null;
    }

    private void ApplyOverrides(Node instance, VisualItemData item)
    {
        // Robustly find the sprite
        var sprite = instance as Sprite2D
                     ?? instance.GetNodeOrNull<Sprite2D>("Sprite")
                     ?? instance.FindChild("*", true, false) as Sprite2D;

        if (sprite != null)
        {
            // Texture Swap
            if (item.TextureOverride != null)
                sprite.Texture = item.TextureOverride;

            // Sprite Sheet Row Selection
            // We only set the Y coordinate. We preserve X so animations don't reset.
            if (item.SpriteSheetRowOverride >= 0)
            {
                sprite.FrameCoords = new Vector2I(sprite.FrameCoords.X, item.SpriteSheetRowOverride);
            }

            // Tint
            if (item.ModulateOverride != Colors.White)
                sprite.Modulate = item.ModulateOverride;
        }
    }

    private IAnimComponent GetAnimComponent(Node node)
    {
        return node as IAnimComponent ?? node.GetNodeOrNull<Node>("AnimationPlayerComponent") as IAnimComponent;
    }
}
