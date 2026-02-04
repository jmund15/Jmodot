namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot.Collections;
using PushinPotions.Global;
using Stats;

/// <summary>
/// Defines a float value that can be computed from a base amount and zero or more
/// Attribute + Operation pairs. Each modifier is applied sequentially.
/// If no modifiers are specified, the base amount is returned as a static value.
/// </summary>
/// <remarks>
/// For simpler use cases, consider using <see cref="ConstantFloatDefinition"/> or
/// <see cref="AttributeFloatDefinition"/> instead.
/// </remarks>
[System.Obsolete("Use ConstantFloatDefinition or AttributeFloatDefinition instead.")]
[GlobalClass, Tool]
public partial class FloatStatDefinition : BaseFloatValueDefinition
{
    /// <summary>
    /// The starting value before any attribute modifiers are applied.
    /// </summary>
    [Export] public float BaseAmount { get; set; } = 10f;

    /// <summary>
    /// An array of Attribute + Operation pairs that modify the base amount.
    /// Applied sequentially in order. Empty array = static value (just BaseAmount).
    /// </summary>
    [Export] public Array<AttributeModifier> Modifiers { get; set; } = new();

    public FloatStatDefinition() { }

    /// <summary>
    /// Constructor for a static value with no attribute modifiers.
    /// </summary>
    public FloatStatDefinition(float baseAmount)
    {
        BaseAmount = baseAmount;
        Modifiers = new Array<AttributeModifier>();
    }

    /// <summary>
    /// Constructor with a single attribute modifier.
    /// </summary>
    public FloatStatDefinition(float baseAmount, Attribute attribute, AttributeOperation operation)
    {
        BaseAmount = baseAmount;
        Modifiers = new Array<AttributeModifier>
        {
            new AttributeModifier { Attribute = attribute, Operation = operation }
        };
    }

    /// <summary>
    /// Constructor for attribute-only value (base = 0, attribute value is the result).
    /// Uses Override operation so the attribute value becomes the final value.
    /// </summary>
    public FloatStatDefinition(Attribute attribute)
    {
        BaseAmount = 0f;
        Modifiers = new Array<AttributeModifier>
        {
            new AttributeModifier { Attribute = attribute, Operation = AttributeOperation.Override }
        };
    }

    /// <summary>
    /// Resolves the final float value by starting with BaseAmount and applying
    /// each modifier sequentially. If no modifiers or no stats, returns BaseAmount.
    /// </summary>
    public override float ResolveFloatValue(IStatProvider? stats)
    {
        float result = BaseAmount;

        if (stats == null || Modifiers.Count == 0)
        {
            return result;
        }

        foreach (var modifier in Modifiers)
        {
            if (modifier?.Attribute == null)
            {
                continue;
            }

            var statVal = stats.GetStatValue<float>(modifier.Attribute);

            result = modifier.Operation switch
            {
                AttributeOperation.Override => statVal,
                AttributeOperation.Add => result + statVal,
                AttributeOperation.Multiply => result * statVal,
                _ => result
            };
        }

        return result;
    }
}
