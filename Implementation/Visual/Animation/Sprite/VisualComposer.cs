namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System.Collections.Generic;
using Godot;
using GCol = Godot.Collections;
using System.Linq;
using Core.Visual.Animation.Sprite;

/// <summary>
/// Manages the visual composition of the character.
/// Handles equipping items into Slots and configuring the CompositeAnimator.
/// </summary>
[GlobalClass]
public partial class VisualComposer : Node
{
    [Export] public CompositeAnimatorComponent CompositeAnimator { get; set; } = null!;
    [Export] public GCol.Array<VisualSlotConfig> SlotConfigs { get; set; } = new();

    private Dictionary<string, VisualSlot> _slots = new();

    public override void _Ready()
    {
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

            // Equip Default
            if (config.DefaultItem != null)
            {
                GD.Print($"VisualComposer: Equiping default item '{config.DefaultItem.Id}' into slot '{config.SlotName}'.");
                slot.Equip(config.DefaultItem);
                GD.Print($"VisualComposer: Equiped default item '{config.DefaultItem.Id}' into slot '{config.SlotName}'.");
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
}
