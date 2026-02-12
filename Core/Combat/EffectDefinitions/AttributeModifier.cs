namespace Jmodot.Core.Combat.EffectDefinitions;

using Jmodot.Core.Shared.Attributes;
using Stats;

/// <summary>
/// A pair of an Attribute and an AttributeOperation.
/// Used by <see cref="ModifiedFloatDefinition"/> to define how each attribute modifies the value.
/// </summary>
[GlobalClass]
public partial class AttributeModifier : Resource
{
    [Export, RequiredExport] public Attribute Attribute { get; set; } = null!;
    [Export] public AttributeOperation Operation { get; set; } = AttributeOperation.Add;
}
