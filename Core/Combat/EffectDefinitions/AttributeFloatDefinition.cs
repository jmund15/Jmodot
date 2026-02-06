namespace Jmodot.Core.Combat.EffectDefinitions;

using Jmodot.Core.Shared.Attributes;
using Stats;

/// <summary>
/// A float value definition that pulls its value directly from a single attribute.
/// Use when the value is entirely determined by a stat (e.g., damage = player's Attack stat).
/// </summary>
[GlobalClass, Tool]
public partial class AttributeFloatDefinition : BaseFloatValueDefinition
{
    /// <summary>
    /// The attribute to read the value from.
    /// </summary>
    [Export, RequiredExport] public Attribute SourceAttribute { get; set; } = null!;

    /// <summary>
    /// The value to return if stats are unavailable or the attribute is not set.
    /// </summary>
    [Export] public float DefaultValue { get; set; } = 0f;

    public AttributeFloatDefinition() { }

    public AttributeFloatDefinition(Attribute attribute, float defaultValue = 0f)
    {
        SourceAttribute = attribute;
        DefaultValue = defaultValue;
    }

    public override float ResolveFloatValue(IStatProvider? stats)
    {
        if (stats == null || SourceAttribute == null)
        {
            return DefaultValue;
        }

        return stats.GetStatValue(SourceAttribute, DefaultValue);
    }
}
