namespace Jmodot.Core.Stats;

using Godot;
using Godot.Collections;
using Jmodot.Core.Modifiers;
using Jmodot.Core.Stats;

[GlobalClass]
public partial class RandomAttributeModifierGroup : Resource
{
    [Export(PropertyHint.TypeString, "Attribute")]
    public Array<Attribute> PossibleAttributes { get; private set; } = new();

    // The modifier that will be applied to the chosen attribute.
    // We are specific with the type because we know these stats are floats.
    // However, if we ever wanted to generalize this for all modifier var types, we would just export "Resource"
    // TODO: Make a base, empty, abstract resource that all Modifier resources implement, then we don't have to export just Resource!
    [Export]
    public FloatAttributeModifier ModifierToApply { get; private set; } = null!;
}
