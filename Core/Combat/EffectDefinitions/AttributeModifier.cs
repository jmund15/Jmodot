namespace Jmodot.Core.Combat.EffectDefinitions;

using Stats;

/// <summary>
/// A pair of an Attribute and an AttributeOperation.
/// Used by FloatEffectDefinition to define how each attribute modifies the value.
/// </summary>
[GlobalClass]
public partial class AttributeModifier : Resource
{
    [Export] public Attribute Attribute { get; set; } = null!;
    [Export] public AttributeOperation Operation { get; set; } = AttributeOperation.Add;
}
