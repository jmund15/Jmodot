namespace Jmodot.Core.Combat.EffectDefinitions;

using PushinPotions.Global;
using Stats;

[GlobalClass]
public partial class FloatEffectDefinition : Resource
{
    [Export] public float BaseAmount { get; set; } = 10f;
    [Export] public Attribute? Attribute { get; set; }
    [Export] public AttributeOperation AttributeOperation { get; set; } = AttributeOperation.Override;

    public FloatEffectDefinition() { }

    public FloatEffectDefinition(float baseAmount)
    {
        BaseAmount = baseAmount;
        Attribute = null;
        AttributeOperation = AttributeOperation.Override; //irrelevant, but can't make nullable for godot reasons
    }
    public FloatEffectDefinition(float baseAmount, Attribute attribute, AttributeOperation attributeOperation)
    {
        BaseAmount = baseAmount;
        Attribute = attribute;
        AttributeOperation = attributeOperation;
    }

    /// <summary>
    /// Constructor for attribute only value (i.e. no base value, stat's value of attribute IS the value)
    /// </summary>
    /// <param name="attribute"></param>
    public FloatEffectDefinition(Attribute attribute)
    {
        BaseAmount = 0f;
        Attribute = attribute;
        AttributeOperation = AttributeOperation.Override;
    }

    /// <summary>
    /// Resolves a float value based on a base value, an optional attribute, and an operation.
    /// </summary>
    public float ResolveFloatValue(IStatProvider? stats)
    {
        if (stats == null || Attribute == null)
        {
            return BaseAmount;
        }

        var statVal = stats.GetStatValue<float>(Attribute);

        return AttributeOperation switch
        {
            AttributeOperation.Override => statVal,
            AttributeOperation.Add => BaseAmount + statVal,
            AttributeOperation.Multiply => BaseAmount * statVal,
            _ => BaseAmount
        };
    }
}
