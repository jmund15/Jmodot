namespace Jmodot.Core.Combat.EffectDefinitions;

using Stats;

/// <summary>
/// A simple constant float value definition.
/// Use when you just need a static number without any stat dependencies.
/// </summary>
[GlobalClass, Tool]
public partial class ConstantFloatDefinition : BaseFloatValueDefinition
{
    /// <summary>
    /// The constant value to return.
    /// </summary>
    [Export] public float Value { get; set; } = 0f;

    public ConstantFloatDefinition() { }

    public ConstantFloatDefinition(float value)
    {
        Value = value;
    }

    public override float ResolveFloatValue(IStatProvider? stats) => Value;
}
