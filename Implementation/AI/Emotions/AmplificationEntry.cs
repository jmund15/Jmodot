namespace Jmodot.Implementation.AI.Emotions;

using Godot;
using Jmodot.Core.AI.Affinities;

/// <summary>
///     Maps a single emotion to its personality-driven amplification.
///     <see cref="LinkedAffinity"/> identifies which personality trait modulates this emotion.
///     <see cref="AmplificationCurve"/> maps affinity value [0,1] → multiplier.
///     Falls back to <see cref="DefaultMultiplier"/> when no curve is assigned.
/// </summary>
[GlobalClass]
public partial class AmplificationEntry : Resource
{
    /// <summary>Which personality trait drives this emotion's amplification.</summary>
    [Export] public Affinity? LinkedAffinity { get; set; }

    /// <summary>
    /// Curve mapping affinity value (X: 0→1) to amplification multiplier (Y).
    /// Example: identity curve (y=x) means "high affinity = high amplification."
    /// A flat-at-2 curve means "double intensity regardless of affinity."
    /// </summary>
    [Export] public Curve? AmplificationCurve { get; set; }

    /// <summary>Fallback multiplier when no curve is assigned. Default 1.0 = no amplification.</summary>
    [Export(PropertyHint.Range, "0,3,0.01")]
    public float DefaultMultiplier { get; set; } = 1.0f;
}
