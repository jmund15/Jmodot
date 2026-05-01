namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot;
using Jmodot.Core.Shared.Attributes;
using Stats;

/// <summary>
/// A float value definition that resolves to a fraction (percentage) of an attribute's value.
/// Composable layer for any "X% of stat Y" computation: spell SelfDamage as % of max-health,
/// healing as % of max-mana, status duration as % of base, etc. Prefer this over consumer-specific
/// "PercentSelfDamageDefinition" / "PercentHealDefinition" — one composable type, many uses.
///
/// Resolved value = stats.GetStatValue(SourceAttribute) * (Percent / 100).
/// </summary>
[GlobalClass, Tool]
public partial class FractionOfStatFloatDefinition : BaseFloatValueDefinition
{
    /// <summary>
    /// The attribute whose value provides the source magnitude.
    /// </summary>
    [Export, RequiredExport] public Attribute SourceAttribute { get; set; } = null!;

    /// <summary>
    /// Percentage of <see cref="SourceAttribute"/>'s value to return. Range allows 0-500%
    /// — values >100 are valid (e.g. healing more than max, scaling-buff multiplier).
    /// </summary>
    [Export(PropertyHint.Range, "0,500,0.1")]
    public float Percent { get; set; } = 10f;

    public FractionOfStatFloatDefinition() { }

    public FractionOfStatFloatDefinition(Attribute attribute, float percent)
    {
        SourceAttribute = attribute;
        Percent = percent;
    }

    public override float ResolveFloatValue(IStatProvider? stats)
    {
        if (stats == null || SourceAttribute == null)
        {
            return 0f;
        }

        float statValue = stats.GetStatValue(SourceAttribute, 0f);
        // Promote to double for the percent multiplication: `Percent * 0.01f` accumulates
        // float-binary error (0.2f = 0.20000000298…), which surfaces as `30 * 20% = 5.999…`
        // when callers expect clean 6.0. The double round-trip eats the artifact.
        return (float)(statValue * (Percent / 100.0));
    }
}
