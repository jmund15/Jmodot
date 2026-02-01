namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;

/// <summary>
/// Configuration for a visual slot (Head, Body, Weapon).
/// </summary>
[GlobalClass]
public partial class VisualSlotConfig : Resource
{
    /// <summary>
    /// Name of the animation category this slot fulfills.
    /// </summary>
    [Export] public string SlotName { get; set; } = "Body";

    /// <summary>
    /// Path relative to the VisualComposer to the node where items are spawned.
    /// </summary>
    [Export]
    public NodePath PathToSlotRoot { get; set; } = null!;

    /// <summary>
    /// If true, this slot dictates the animation timing for the entire character.
    /// Usually TRUE for Body, FALSE for Hats/Weapons.
    /// </summary>
    [Export] public bool IsTimeSource { get; set; } = false;

    /// <summary>
    /// If false, Unequip() will fail unless replaced by another item.
    /// </summary>
    [Export] public bool IsOptional { get; set; } = true;

    /// <summary>
    /// If true, this slot's animator is NOT registered with the CompositeAnimator.
    /// Use for slots that play their own animations independently of the body
    /// (e.g., one-shot overlay animations like PotionAdd).
    /// </summary>
    [Export] public bool IsAnimationIndependent { get; set; } = false;

    /// <summary>
    /// Automatically equipped on Start if no save data is present.
    /// </summary>
    [Export] public VisualItemData? DefaultItem { get; set; }
}
