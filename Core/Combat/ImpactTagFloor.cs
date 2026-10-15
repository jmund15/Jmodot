namespace Jmodot.Core.Combat;

using Shared.Attributes;

/// <summary>
/// A minimum <see cref="Reactions.HitReport.Weight"/> a hit carrying <see cref="Tag"/> reads at,
/// however little damage it did: a heavy blow stays heavy against a tough victim.
/// </summary>
[GlobalClass, Tool]
public partial class ImpactTagFloor : Resource
{
    /// <summary>The tag that raises the floor. Matches a hit's tags by <see cref="CombatTag.TagId"/>.</summary>
    [Export, RequiredExport] public CombatTag Tag { get; set; } = null!;

    /// <summary>The weight floor in [0, 1] this tag gives a hit.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float Floor { get; set; }
}
