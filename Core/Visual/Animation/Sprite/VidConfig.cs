namespace Jmodot.Core.Visual.Animation.Sprite;

using Jmodot.Core.Shared.Attributes;

[GlobalClass]
public partial class VidConfig : Resource
{
    [Export, RequiredExport] public StringName SlotName { get; private set; } = null!;
    /// <summary>
    /// If null, that indicates to Unequip!
    /// </summary>
    [Export] public VisualItemData? VidToEquip { get; private set; }

}
