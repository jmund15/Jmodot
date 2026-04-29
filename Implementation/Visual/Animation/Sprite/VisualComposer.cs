namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Visual;
using Jmodot.Core.Visual.Animation.Sprite;
using Jmodot.Core.Visual.Effects;
using Jmodot.Implementation.Visual.Effects;
using Shared;

/// <summary>
/// Coordinator over a set of <see cref="VisualSlotNode"/> scene-graph children.
/// Replaces the legacy config-array model.
/// </summary>
/// <remarks>
/// <para>
/// The composer is a thin aggregator: it discovers all child <see cref="VisualSlotNode"/>s
/// at <c>_Ready</c>, validates uniqueness of <c>SlotKey.Id</c>, wires each slot's
/// dependencies (composite animator + effect service), and forwards each slot's
/// <see cref="IVisualNodeProvider"/> events 1:1 to its own observers (D1/D2 forwarding —
/// no aggregate cache, queries hit slots on demand).
/// </para>
/// <para>
/// Slot-level events fire AFTER the slot has updated its internal state, so subscribers
/// querying <see cref="GetVisualNodes"/> from inside an event handler always see
/// consistent state. This eliminates the <c>CallDeferred</c> coherency workaround
/// that the legacy composer required.
/// </para>
/// <para>
/// <b>Static composition only:</b> slot membership is fixed at <c>_Ready</c>. Reparenting
/// a <see cref="VisualSlotNode"/> away from the composer at runtime is unsupported —
/// the composer keeps event subscriptions to its discovered slots until <c>_ExitTree</c>
/// and will keep forwarding events from a removed slot. If runtime slot composition
/// becomes a need, hook <c>ChildExitingTree</c> on the composer and unsubscribe + remove
/// from <see cref="_slotsByKey"/> / <see cref="_slots"/>.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class VisualComposer : Node3D, IVisualNodeProvider
{
    [Export, RequiredExport] public CompositeAnimatorComponent CompositeAnimator { get; set; } = null!;

    /// <summary>
    /// The effect service that owns base-color and persistent-tint state. Wired
    /// by the designer; the composer subscribes the service to its own provider
    /// events on <c>_Ready</c>.
    /// </summary>
    [Export, RequiredExport] public VisualEffectService Effects { get; set; } = null!;

    private readonly Dictionary<StringName, VisualSlotNode> _slotsByKey = new();
    private readonly List<VisualSlotNode> _slots = new();

    public IReadOnlyList<VisualSlotNode> Slots => _slots;

    public event Action<VisualNodeHandle> NodeAdded = delegate { };
    public event Action<VisualNodeHandle> NodeRemoved = delegate { };
    public event Action<VisualNodeHandle> NodeVisibilityChanged = delegate { };

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { return; }
        this.ValidateRequiredExports();

        DiscoverSlots();

        // Wire effect service to receive our provider events BEFORE we equip defaults
        // so persistent tints registered during default-equip apply correctly.
        Effects.AttachToProvider(this);

        // Default-item equip is deferred — _Ready is called while the parent's
        // tree is locked, so AddChild from inside Equip would throw.
        CallDeferred(MethodName.EquipAllDefaults);
    }

    private void DiscoverSlots()
    {
        foreach (var child in GetChildren())
        {
            if (child is VisualSlotNode slot)
            {
                if (slot.Key == null)
                {
                    JmoLogger.Error(this, $"VisualComposer: child slot '{slot.Name}' has no Key. Slot ignored.");
                    continue;
                }
                if (_slotsByKey.ContainsKey(slot.Key.Id))
                {
                    JmoLogger.Error(this, $"VisualComposer: duplicate SlotKey.Id '{slot.Key.Id}' (slot '{slot.Name}'). Slot ignored.");
                    continue;
                }
                _slotsByKey[slot.Key.Id] = slot;
                _slots.Add(slot);
                slot.Initialize(CompositeAnimator, Effects);
                slot.NodeAdded += OnSlotNodeAdded;
                slot.NodeRemoved += OnSlotNodeRemoved;
                slot.NodeVisibilityChanged += OnSlotNodeVisibilityChanged;
            }
        }
    }

    public override void _ExitTree()
    {
        foreach (var slot in _slots)
        {
            slot.NodeAdded -= OnSlotNodeAdded;
            slot.NodeRemoved -= OnSlotNodeRemoved;
            slot.NodeVisibilityChanged -= OnSlotNodeVisibilityChanged;
        }
    }

    private void EquipAllDefaults()
    {
        foreach (var slot in _slots)
        {
            if (slot.CurrentItem == null && slot.DefaultItem != null)
            {
                slot.Equip(slot.DefaultItem);
            }
        }
    }

    private void OnSlotNodeAdded(VisualNodeHandle h) => NodeAdded?.Invoke(h);
    private void OnSlotNodeRemoved(VisualNodeHandle h) => NodeRemoved?.Invoke(h);
    private void OnSlotNodeVisibilityChanged(VisualNodeHandle h) => NodeVisibilityChanged?.Invoke(h);

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (CompositeAnimator == null) { warnings.Add("CompositeAnimator export is required."); }
        if (Effects == null) { warnings.Add("Effects (VisualEffectService) export is required."); }

        var seenKeys = new HashSet<StringName>();
        bool hasMaster = false;
        foreach (var child in GetChildren())
        {
            if (child is VisualSlotNode slot)
            {
                if (slot.Key == null) { continue; }
                if (!seenKeys.Add(slot.Key.Id))
                {
                    warnings.Add($"Duplicate SlotKey.Id '{slot.Key.Id}' on slot '{slot.Name}'.");
                }
                if (slot.SyncMode == AnimationSyncMode.Master) { hasMaster = true; }
            }
        }
        if (seenKeys.Count > 0 && !hasMaster)
        {
            warnings.Add("No slot has SyncMode=Master. CompositeAnimator will run masterless — IsPlaying/HasAnimation/etc. return defaults until a Master slot is added. Mark exactly one slot as Master.");
        }
        return warnings.ToArray();
    }

    #region Typed slot facade

    public VisualSlotNode? GetSlot(SlotKey key)
        => key != null && _slotsByKey.TryGetValue(key.Id, out var slot) ? slot : null;

    public bool TryGetSlot(SlotKey key, out VisualSlotNode slot)
    {
        if (key != null && _slotsByKey.TryGetValue(key.Id, out var found))
        {
            slot = found;
            return true;
        }
        slot = null!;
        return false;
    }

    public VisualEquipResult Equip(SlotKey key, VisualItemData item)
    {
        if (TryGetSlot(key, out var slot)) { return slot.Equip(item); }
        var msg = $"VisualComposer: no slot for key '{key?.Id}' on Equip.";
#if TOOLS
        if (!Engine.IsEditorHint()) { throw new InvalidOperationException(msg); }
#endif
        JmoLogger.Error(this, msg);
        return VisualEquipResult.Failed(key);
    }

    public void Unequip(SlotKey key)
    {
        if (TryGetSlot(key, out var slot)) { slot.Unequip(); return; }
#if TOOLS
        if (!Engine.IsEditorHint()) { throw new InvalidOperationException($"VisualComposer: no slot for key '{key?.Id}' on Unequip."); }
#endif
        JmoLogger.Error(this, $"VisualComposer: no slot for key '{key?.Id}' on Unequip.");
    }

    public VisualItemData? GetEquippedItem(SlotKey key)
        => TryGetSlot(key, out var slot) ? slot.CurrentItem : null;

    public VisualEquipResult Push(SlotKey key, VisualItemData item, PushOptions options = PushOptions.None)
    {
        if (TryGetSlot(key, out var slot)) { return slot.Push(item, options); }
        var msg = $"VisualComposer: no slot for key '{key?.Id}' on Push.";
#if TOOLS
        if (!Engine.IsEditorHint()) { throw new InvalidOperationException(msg); }
#endif
        JmoLogger.Error(this, msg);
        return VisualEquipResult.Failed(key);
    }

    public void Pop(SlotKey key)
    {
        if (TryGetSlot(key, out var slot)) { slot.Pop(); return; }
#if TOOLS
        if (!Engine.IsEditorHint()) { throw new InvalidOperationException($"VisualComposer: no slot for key '{key?.Id}' on Pop."); }
#endif
        JmoLogger.Error(this, $"VisualComposer: no slot for key '{key?.Id}' on Pop.");
    }

    #endregion

    #region IVisualNodeProvider — D1/D2 forwarding (no aggregate cache)

    public IReadOnlyList<VisualNodeHandle> GetVisualNodes(VisualQuery query)
    {
        var results = new List<VisualNodeHandle>();
        foreach (var slot in _slots)
        {
            results.AddRange(slot.GetVisualNodes(query));
        }
        return results;
    }

    public IReadOnlyList<VisualNodeHandle> GetVisibleNodes(VisualQuery query)
    {
        var results = new List<VisualNodeHandle>();
        foreach (var slot in _slots)
        {
            results.AddRange(slot.GetVisibleNodes(query));
        }
        return results;
    }

    #endregion
}
