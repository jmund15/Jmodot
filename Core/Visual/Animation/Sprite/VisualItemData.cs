namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;

/// <summary>
/// Defines a specific visual item (Sword, Hat, Body Type).
/// Supports unique prefabs, texture swaps, and sprite sheet row selection.
/// </summary>
[GlobalClass]
public partial class VisualItemData : Resource
{
    [Export] public string Id { get; set; } = null!;

    /// <summary>
    /// The scene to instantiate. Must contain an IAnimComponent (AnimationPlayer).
    /// </summary>
    [ExportGroup("Core")]
    [Export] public PackedScene Prefab { get; set; } = null!;

    /// <summary>
    /// Optional: Swaps the texture of the first found Sprite2D.
    /// Use this for reskins (e.g. Blue Hat vs Red Hat) sharing the same Prefab.
    /// </summary>
    [ExportGroup("Overrides")]
    [Export] public Texture2D? TextureOverride { get; set; }

    /// <summary>
    /// Optional: Forces the Sprite to use a specific vertical row (Y).
    /// Use this for Sprite Sheets where Rows = Items and Columns = Animation Frames.
    /// Set to -1 to disable.
    /// NOTE: Your AnimationPlayer must ONLY key 'frame_coords:x', not the full vector.
    /// </summary>
    [Export] public int SpriteSheetRowOverride { get; set; } = -1;

    /// <summary>
    /// Optional: Tints the sprite (e.g. Dye system).
    /// </summary>
    [Export] public Color ModulateOverride { get; set; } = Colors.White;
}
