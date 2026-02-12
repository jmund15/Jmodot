namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot.Collections;
using PushinPotions.Global;
using Stats;

/// <summary>
/// Defines a float value computed from a base amount modified by zero or more
/// Attribute + Operation pairs applied sequentially.
/// Use when a value needs stat-driven scaling (e.g., base damage * effect intensity).
/// For static values use <see cref="ConstantFloatDefinition"/>;
/// for pure attribute reads use <see cref="AttributeFloatDefinition"/>.
/// </summary>
[GlobalClass, Tool]
public partial class ModifiedFloatDefinition : BaseFloatValueDefinition
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

    public ModifiedFloatDefinition() { }

    /// <summary>
    /// Constructor for a static value with no attribute modifiers.
    /// </summary>
    public ModifiedFloatDefinition(float baseAmount)
    {
        BaseAmount = baseAmount;
        Modifiers = new Array<AttributeModifier>();
    }

    /// <summary>
    /// Constructor with a single attribute modifier.
    /// </summary>
    public ModifiedFloatDefinition(float baseAmount, Attribute attribute, AttributeOperation operation)
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
    public ModifiedFloatDefinition(Attribute attribute)
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
