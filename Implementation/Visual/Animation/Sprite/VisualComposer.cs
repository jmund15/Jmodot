namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System.Collections.Generic;
using Godot;
using GCol = Godot.Collections;
using System.Linq;
using Core.Visual.Animation.Sprite;

using System;
using Core.Movement;
using Core.Visual.Effects;
using Shared;

/// <summary>
/// Manages the visual composition of the character.
/// Handles equipping items into Slots and configuring the CompositeAnimator.
/// </summary>
[GlobalClass, Tool]
public partial class VisualComposer : Node, IVisualSpriteProvider
{
    [Export] public CompositeAnimatorComponent CompositeAnimator { get; set; } = null!;
    [Export] public GCol.Array<VisualSlotConfig> SlotConfigs { get; set; } = new();

    private Dictionary<string, VisualSlot> _slots = new();

    private List<Node> _visibleNodes;
    private List<Node> _visualNodes;

    [ExportGroup("Debug")]
    [Export] private bool _useFlipHDebug = true;
    [Export] private AnimationOrchestrator? _debugOrchestrator;
    [Export] private DirectionSet2D? _flipHDirSet;
    private bool _noDirActive = false;

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }
        // Initialize Slots based on Configs
        foreach (var config in SlotConfigs)
        {
            Node slotRoot = GetNodeOrNull(config.PathToSlotRoot);
            if (slotRoot == null)
            {
                GD.PrintErr($"VisualComposer: Invalid path '{config.PathToSlotRoot}' for slot '{config.SlotName}'.");
                continue;
            }

            var slot = new VisualSlot(config, CompositeAnimator, slotRoot);
            _slots[config.SlotName] = slot;

            // Subscribe to effects changes for bubbling
            slot.VisualNodesChanged += OnSlotVisualNodesChanged;
            slot.VisibleNodesChanged += OnSlotVisibleNodesChanged;

            // Equip Default
            if (config.DefaultItem != null)
            {
                // Defer the initial equip to avoid "Parent is busy setting up children" errors
                // since we are currently inside _Ready and the scene tree is locked.
                CallDeferred(MethodName.EquipDefault, config.SlotName, config.DefaultItem);
                GD.Print($"VisualComposer: Scheduled default item '{config.DefaultItem.Id}' for slot '{config.SlotName}' (Deferred).");
            }
        }

        if (_useFlipHDebug)
        {
            _debugOrchestrator.AnimStarted += OnOrchAnimStarted;
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (Engine.IsEditorHint()) { return; }
        if (!_useFlipHDebug)
        {
            return;
        }
        var flipHDir = _flipHDirSet.GetClosestDirection(
            _debugOrchestrator.CurrentAnimationDirection.GetFlattenedVector2());
        if (flipHDir == Vector2.Right)
        {
            DebugSetFlipH(false);
        }
        else
        {
            DebugSetFlipH(true);
        }
    }

    private void OnOrchAnimStarted(StringName obj)
    {
        var flipHDir = _flipHDirSet.GetClosestDirection(
                _debugOrchestrator.CurrentAnimationDirection.GetFlattenedVector2());
        if (flipHDir == Vector2.Right)
        {
            DebugSetFlipH(false);
        }
        else
        {
            DebugSetFlipH(true);
        }
    }

    private void DebugSetFlipH(bool flip)
    {
        foreach (var visualNode in GetAllVisualNodes())
        {
            if (visualNode is SpriteBase3D sprite3D)
            {
                sprite3D.FlipH = flip;
            }
        }
    }

    /// <summary>
    /// Helper used by _Ready to safely equip default items.
    /// Only equips if the slot is still empty (null), respecting any setup done by other components (e.g. HSM) during initialization.
    /// </summary>
    private void EquipDefault(string slotName, VisualItemData item)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            // Only equip default if nothing else has claimed the slot yet
            if (slot.CurrentItem == null)
            {
                slot.Equip(item);
                GD.Print($"VisualComposer: Executing deferred default equip for '{slotName}' -> '{item.Id}'");
            }
            else
            {
                GD.Print($"VisualComposer: Skipping deferred default equip for '{slotName}'. Slot already has '{slot.CurrentItem.Id}'.");
            }
        }
    }

    public void Equip(string slotName, VisualItemData item)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            slot.Equip(item);
        }
        else
        {
            GD.PrintErr($"VisualComposer: Slot '{slotName}' not found.");
        }
    }

    public void Unequip(string slotName)
    {
        if (_slots.TryGetValue(slotName, out var slot))
        {
            slot.Unequip();
        }
    }

    public VisualItemData? GetEquippedItem(string slotName)
    {
        return _slots.TryGetValue(slotName, out var slot) ? slot.CurrentItem : null;
    }

    #region IVisualSpriteProvider Implementation

    public event Action VisibleNodesChanged;
    public event Action VisualNodesChanged;

    public IReadOnlyList<Node> GetVisibleNodes()
    {
        return _visibleNodes;
    }

    public IReadOnlyList<Node> GetAllVisualNodes()
    {
        return _visualNodes;
    }

    private void OnSlotVisualNodesChanged()
    {
        VisibleNodesChanged?.Invoke();
        VisualNodesChanged?.Invoke();

        _visualNodes = new List<Node>();
        foreach (var slot in _slots.Values)
        {
            _visualNodes.AddRange(slot.GetCurrentVisualNodes());
        }

        _visibleNodes = new List<Node>();
        foreach (var slot in _slots.Values)
        {
            _visibleNodes.AddRange(slot.GetCurrentVisibleNodes());
        }
    }
    private void OnSlotVisibleNodesChanged()
    {
        VisibleNodesChanged?.Invoke();

        _visibleNodes = new List<Node>();
        foreach (var slot in _slots.Values)
        {
            _visibleNodes.AddRange(slot.GetCurrentVisibleNodes());
        }
    }

    #endregion
}
