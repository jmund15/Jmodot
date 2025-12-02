namespace Jmodot.Core.Combat;

using Stats;

/// <summary>
/// Base class for Combat Effect Creation.
/// Desiging the factory/create pattern allows for resource to be stateless
/// while maintaining resource/exportability in the Godot editor
/// </summary>
[GlobalClass]
public abstract partial class CombatEffectFactory : Resource, ICombatEffectFactory
{
    public enum StatOperation
    {
        Override,
        Add,
        Multiply
    }

    public abstract ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null);

    /// <summary>
    /// Resolves a float value based on a base value, an optional attribute, and an operation.
    /// </summary>
    protected float ResolveFloatValue(float baseVal, Attribute? attr, StatOperation op, Jmodot.Core.Stats.IStatProvider? stats)
    {
        if (stats == null || attr == null)
        {
            return baseVal;
        }

        float statVal = stats.GetStatValue<float>(attr);

        return op switch
        {
            StatOperation.Override => statVal,
            StatOperation.Add => baseVal + statVal,
            StatOperation.Multiply => baseVal * statVal,
            _ => baseVal
        };
    }
}
