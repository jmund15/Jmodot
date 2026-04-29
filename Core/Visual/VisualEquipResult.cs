namespace Jmodot.Core.Visual;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.Visual.Animation.Sprite;

/// <summary>
/// Result of a slot equip / push operation. Carries everything a caller needs to
/// access the freshly-instantiated visuals without re-querying the slot.
/// </summary>
public sealed record VisualEquipResult(
    bool Success,
    SlotKey SlotKey,
    Node? Instance,
    IAnimComponent? Animator,
    IVisualNodeProvider? Provider,
    IReadOnlyList<VisualNodeHandle> Handles)
{
    public static VisualEquipResult Failed(SlotKey slotKey)
        => new(false, slotKey, null, null, null, System.Array.Empty<VisualNodeHandle>());
}
