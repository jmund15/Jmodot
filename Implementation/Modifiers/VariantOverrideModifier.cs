namespace Jmodot.Implementation.Modifiers;

using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.StageRules;

/// <summary>
/// A general-purpose modifier that directly overrides the value of any Variant stat.
/// Its effect is determined by its Priority relative to other override modifiers.
/// </summary>
[GlobalClass, Tool]
public partial class VariantOverrideModifier : AttributeModifier, IVariantModifier
{
    [Export] public Variant Value { get; private set; }

    [Export] public VariantModifierStageRule StageRule { get; private set; }

    public VariantOverrideModifier()
    {
        StageRule = new VariantOverrideStageRule();
    }
}
