namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;
using GCol = Godot.Collections;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// Per-part binding declared by a prefab via <see cref="VisualRig"/>. Identifies a
/// specific child node by path, attaches a <see cref="PartId"/> and tags, and gates
/// which <c>VisualItemData</c> overrides apply to it.
/// </summary>
[GlobalClass]
public partial class VisualPartBinding : Resource
{
    /// <summary>
    /// Path to the target visual node, relative to the prefab's root.
    /// </summary>
    [Export, RequiredExport] public NodePath TargetNode { get; set; } = null!;

    /// <summary>
    /// Optional identifier for this part within the slot. Lets queries target specific
    /// parts (e.g. "Blade", "Hilt"). Null means the binding contributes a node but is
    /// not addressable by part name.
    /// </summary>
    [Export] public StringName? PartId { get; set; }

    /// <summary>
    /// Tags applied to the produced <c>VisualNodeHandle</c>. Tags are the primary way
    /// queries select cross-slot groupings (e.g. "PlayerColored", "Metallic").
    /// </summary>
    [Export] public GCol.Array<StringName> Tags { get; set; } = new();

    /// <summary>
    /// If false, this part is excluded from effect application (tints, flashes).
    /// </summary>
    [Export] public bool EffectEligible { get; set; } = true;

    /// <summary>
    /// If true, this part's visibility is tracked separately from the always-visible set.
    /// Used by visibility-driven providers (e.g. <c>AnimationVisibilityCoordinator</c>).
    /// </summary>
    [Export] public bool VisibilityParticipant { get; set; } = true;

    /// <summary>
    /// If true, <c>VisualItemData.TextureOverride</c> applies to this part.
    /// </summary>
    [Export] public bool ReceivesTextureOverride { get; set; } = true;

    /// <summary>
    /// If true, <c>VisualItemData.SpriteSheetRowOverride</c> applies to this part.
    /// </summary>
    [Export] public bool ReceivesRowOverride { get; set; } = true;

    /// <summary>
    /// If true, <c>VisualItemData.ModulateOverride</c> applies to this part.
    /// </summary>
    [Export] public bool ReceivesModulateOverride { get; set; } = true;
}
