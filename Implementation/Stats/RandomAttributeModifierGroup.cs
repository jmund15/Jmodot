namespace Jmodot.Core.Stats;

using Godot;
using Godot.Collections;
using Jmodot.Core.Modifiers;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;

[GlobalClass, Tool]
public partial class RandomAttributeModifierGroup : Resource
{
    [Export] public Array<Attribute> PossibleAttributes { get; private set; } = new();

    [Export, RequiredExport]
    public AttributeModifier ModifierToApply { get; private set; } = null!;

    #region Test Helpers
#if TOOLS
    internal void SetPossibleAttributes(Godot.Collections.Array<Attribute> value) => PossibleAttributes = value;
    internal void SetModifierToApply(AttributeModifier value) => ModifierToApply = value;
#endif
    #endregion
}
