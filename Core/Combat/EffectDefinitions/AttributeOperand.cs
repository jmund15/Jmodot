namespace Jmodot.Core.Combat.EffectDefinitions;

using Jmodot.Core.Shared.Attributes;
using Stats;

/// <summary>
/// A pair of an Attribute and an AttributeOperation — one operand of a <see cref="ModifiedFloatDefinition"/>,
/// defining how that attribute modifies the value.
/// </summary>
[GlobalClass, Tool]
public partial class AttributeOperand : Resource
{
    [Export, RequiredExport] public Attribute Attribute { get; set; } = null!;
    [Export] public AttributeOperation Operation { get; set; } = AttributeOperation.Add;
}
