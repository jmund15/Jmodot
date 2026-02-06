namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot;
using Stats;

/// <summary>
/// An integer value definition that pulls its value directly from a single attribute.
/// Use when the value is entirely determined by a stat (e.g., pierce count = player's Pierce stat).
/// Mirrors <see cref="AttributeFloatDefinition"/> for integer use cases.
/// </summary>
[Tool]
[GlobalClass]
public partial class AttributeIntDefinition : BaseIntValueDefinition
{
    /// <summary>
    /// The attribute to read the value from.
    /// </summary>
    [Export] public Attribute SourceAttribute { get; set; } = null!;

    /// <summary>
    /// The value to return if stats are unavailable or the attribute is not set.
    /// </summary>
    [Export] public int DefaultValue { get; set; } = 0;

    public AttributeIntDefinition() { }

    public AttributeIntDefinition(Attribute attribute, int defaultValue = 0)
    {
        SourceAttribute = attribute;
        DefaultValue = defaultValue;
    }

    public override int ResolveIntValue(IStatProvider? stats)
    {
        if (stats == null || SourceAttribute == null)
        {
            return DefaultValue;
        }

        return stats.GetStatValue(SourceAttribute, DefaultValue);
    }
}
