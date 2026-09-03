namespace Jmodot.Core.Modifiers;

using Jmodot.Core.Modifiers.StageRules;

/// <summary>
///     Resource for flipping an attribute's bool value. Folds via a flip stage (toggles once per modifier);
///     <see cref="Value" /> is unused by the flip rule but present to satisfy the IBoolModifier contract.
/// </summary>
[GlobalClass, Tool]
public partial class BoolFlipAttributeModifier : AttributeModifier, IBoolModifier
{
    [Export] public bool Value { get; private set; }

    [Export] public BoolModifierStageRule StageRule { get; private set; }

    public BoolFlipAttributeModifier()
    {
        StageRule = new BoolFlipStageRule();
    }

    public BoolFlipAttributeModifier(int priority) : this()
    {
        Priority = priority;
    }
}
