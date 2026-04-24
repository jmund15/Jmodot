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
using Jmodot.Core.Shared.Attributes;
using Effects;

/// <summary>
/// Manages the visual composition of the character.
/// Handles equipping items into Slots and configuring the CompositeAnimator.
/// </summary>
[GlobalClass, Tool]
public partial class VisualComposer : Node, IVisualSpriteProvider
{
    [Export, RequiredExport] public CompositeAnimatorComponent CompositeAnimator { get; set; } = null!;
    [Export] public GCol.Array<VisualSlotConfig> SlotConfigs { get; set; } = new();

    private Dictionary<string, VisualSlot> _slots = new();

    private VisualEffectService? _effects;

    /// <summary>
    /// Facade for scoped base-tint and future transient-effect application. Owns the
    /// per-node base-color dictionary (formerly <c>BaseModulationTracker</c>, now
    /// folded in). Created lazily so tests and editor tools that don't exercise
    /// equipment pay no cost, but guaranteed ready by <see cref="ConfigureSlots"/>
    /// so slots can register sprites on Equip.
    /// </summary>
    /// <remarks>
    /// Phase 4.5 — replaces the manual "touch BaseColorTracker then call
    /// VisualEffectController.RefreshVisualNodes" dance previously required of every
    /// consumer that wanted to tint equipment.
    /// Phase 4.6 — absorbed <c>BaseModulationTracker</c>; there is no longer a
    /// separate tracker class.
    /// </remarks>
    public IVisualEffectService Effects => _effects ??= new VisualEffectService(this);

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
        this.ValidateRequiredExports();
        ConfigureSlots();

        // Equip Default (deferred — scene tree is locked during _Ready)
        foreach (var config in SlotConfigs)
        {
            if (config.DefaultItem != null)
            {
                CallDeferred(MethodName.EquipDefault, config.SlotName, config.DefaultItem);
            }
        }

        if (_useFlipHDebug && _debugOrchestrator != null && _flipHDirSet != null)
        {
            _debugOrchestrator.AnimStarted += OnOrchAnimStarted;
        }
    }

    /// <summary>
    /// Instantiates <see cref="VisualSlot"/>s from <see cref="SlotConfigs"/> and wires
    /// their change events through to this composer's own events. Separated from
    /// <see cref="_Ready"/> so tests (and future non-lifecycle callers) can spin up
    /// a composer without entering the scene tree.
    /// </summary>
    /// <remarks>
    /// Does NOT equip default items — that's deferred by <see cref="_Ready"/> because
    /// Godot's tree is locked during _Ready and the item's prefab instantiation would
    /// throw. Tests that need defaults equipped can call <see cref="Equip"/> directly
    /// after this.
    /// </remarks>
    internal void ConfigureSlots()
    {
        // Eager-create the service so slots register sprites on Equip synchronously —
        // avoids a lazy-init race where a slot's ApplyOverrides fires before any
        // consumer has touched `Effects`.
        _effects ??= new VisualEffectService(this);

        foreach (var config in SlotConfigs)
        {
            Node slotRoot = GetNodeOrNull(config.PathToSlotRoot);
            if (slotRoot == null)
            {
                JmoLogger.Error(this, $"VisualComposer: Invalid path '{config.PathToSlotRoot}' for slot '{config.SlotName}'.");
                continue;
            }

            var slot = new VisualSlot(config, CompositeAnimator, slotRoot, _effects);
            _slots[config.SlotName] = slot;

            slot.VisualNodesChanged += OnSlotVisualNodesChanged;
            slot.VisibleNodesChanged += OnSlotVisibleNodesChanged;
        }
    }

    public override void _ExitTree()
    {
        if (_debugOrchestrator != null)
        {
            _debugOrchestrator.AnimStarted -= OnOrchAnimStarted;
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (Engine.IsEditorHint()) { return; }
        if (!_useFlipHDebug || _debugOrchestrator == null || _flipHDirSet == null)
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
        // Guaranteed non-null: this callback only subscribed when _debugOrchestrator != null
        var flipHDir = _flipHDirSet!.GetClosestDirection(
                _debugOrchestrator!.CurrentAnimationDirection.GetFlattenedVector2());
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
                //JmoLogger.Info(this, $"VisualComposer: Executing deferred default equip for '{slotName}' -> '{item.Id}'");
            }
            else
            {
                //JmoLogger.Info(this, $"VisualComposer: Skipping deferred default equip for '{slotName}'. Slot already has '{slot.CurrentItem.Id}'.");
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
           JmoLogger.Error(this, $"VisualComposer: Slot '{slotName}' not found.");
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

    /// <summary>
    /// Returns visual nodes belonging to a specific named slot.
    /// Returns empty list if slot doesn't exist or has no visual nodes.
    /// </summary>
    public IReadOnlyList<Node> GetVisualNodesForSlot(string slotName)
    {
        return _slots.TryGetValue(slotName, out var slot)
            ? slot.GetAllVisualNodes()
            : [];
    }

    /// <summary>
    /// Looks up a slot by name and returns it as a <see cref="VisualSlot"/> handle.
    /// Callers get the slot's <see cref="IVisualSpriteProvider"/> surface, its animator,
    /// and the current item — enabling per-slot subscription and animator access without
    /// string-searching the scene tree.
    /// </summary>
    public bool TryGetSlot(string slotName, out VisualSlot? slot)
    {
        return _slots.TryGetValue(slotName, out slot);
    }

    #region IVisualSpriteProvider Implementation

    public event Action VisibleNodesChanged = delegate { };
    public event Action VisualNodesChanged = delegate { };

    /// <summary>
    /// Pull-on-read aggregate of every slot's visible nodes. Not cached — the slots
    /// already own the cheap in-memory lists, so repeated aggregation per event is
    /// trivial compared to the invalidation/ordering bugs a composer-level cache
    /// previously caused.
    /// </summary>
    public IReadOnlyList<Node> GetVisibleNodes()
    {
        var result = new List<Node>();
        foreach (var slot in _slots.Values)
        {
            result.AddRange(slot.GetVisibleNodes());
        }
        return result;
    }

    /// <summary>
    /// Pull-on-read aggregate of every slot's visual nodes. See <see cref="GetVisibleNodes"/>
    /// for the no-cache rationale.
    /// </summary>
    public IReadOnlyList<Node> GetAllVisualNodes()
    {
        var result = new List<Node>();
        foreach (var slot in _slots.Values)
        {
            result.AddRange(slot.GetAllVisualNodes());
        }
        return result;
    }

    // 1:1 event forwarding. A slot's Visual fire bubbles ONLY the composer's Visual
    // event; a Visible fire bubbles ONLY Visible. Prior cross-firing (Visual fire
    // also firing Visible) caused 2× Visible events per Equip — see D1 pin tests.
    private void OnSlotVisualNodesChanged() => VisualNodesChanged.Invoke();
    private void OnSlotVisibleNodesChanged() => VisibleNodesChanged.Invoke();

    #endregion
}
