namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot;
using Stats;

/// <summary>
/// A simple constant integer value definition.
/// Use when you just need a static number without any stat dependencies.
/// Mirrors <see cref="ConstantFloatDefinition"/> for integer use cases.
/// </summary>
[Tool]
[GlobalClass]
public partial class ConstantIntDefinition : BaseIntValueDefinition
{
    /// <summary>
    /// The constant value to return.
    /// </summary>
    [Export] public int Value { get; set; } = 0;

    public ConstantIntDefinition() { }

    public ConstantIntDefinition(int value)
    {
        Value = value;
    }

    public override int ResolveIntValue(IStatProvider? stats) => Value;
}
