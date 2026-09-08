namespace Jmodot.Core.Modifiers;

using Jmodot.Core.Modifiers.StageRules;

/// <summary>
///     Resource for modifying a bool value. Defaults to an override fold (highest-priority value wins).
/// </summary>
[GlobalClass, Tool]
public partial class BoolAttributeModifier : AttributeModifier, IBoolModifier
{
    [Export] public bool Value { get; private set; }

    [Export] public BoolModifierStageRule StageRule { get; private set; }

    public BoolAttributeModifier()
    {
        StageRule = new BoolOverrideStageRule();
    }

    public BoolAttributeModifier(bool value, int priority) : this()
    {
        Value = value;
        Priority = priority;
    }
}
