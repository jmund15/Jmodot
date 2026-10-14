namespace Jmodot.Core.Combat;

/// <summary>
/// The numbers that turn damage, push and tags into a hit's <see cref="Reactions.HitReport.Weight"/>
/// and <see cref="Reactions.ImpactReport.Magnitude"/>. The consuming project wires one through
/// <see cref="CombatFactoryDefaults.ImpactWeight"/>.
/// </summary>
[GlobalClass, Tool]
public partial class ImpactWeightProfile : Resource
{
    /// <summary>Requested damage that reads as a full-size spell (Magnitude 1).</summary>
    [Export(PropertyHint.Range, "0.01,1000,0.01,or_greater")] public float ReferenceDamage { get; set; } = 50f;

    /// <summary>Producer impulse in N·s that reads as a full-size spell (Magnitude 1).</summary>
    [Export(PropertyHint.Range, "0.01,1000,0.01,or_greater")] public float ReferenceImpulse { get; set; } = 10f;

    /// <summary>Applied velocity change in m/s that reads as a full push (PushFraction 1).</summary>
    [Export(PropertyHint.Range, "0.01,100,0.01,or_greater")] public float ReferenceDeltaV { get; set; } = 8f;

    /// <summary>Weight multiplier for a critical hit, at least 1.</summary>
    [Export(PropertyHint.Range, "1,5,0.01,or_greater")] public float CritMultiplier { get; set; } = 1.5f;

    /// <summary>Per-tag minimum weights; the largest floor among a hit's tags applies.</summary>
    [Export] public Godot.Collections.Array<ImpactTagFloor> TagFloors { get; set; } = new();

    /// <summary>Physics frames before a death within which the last hit counts as its cause.</summary>
    [Export(PropertyHint.Range, "0,120,1,or_greater")] public int DeathPairWindowFrames { get; set; } = 15;

    /// <summary>Magnitude of a death no recent hit explains, in [0, 1].</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float DefaultDeathMagnitude { get; set; } = 0.3f;

    /// <summary>Absolute size in [0, 1] of a requested damage and impulse, the larger axis winning.</summary>
    public float MagnitudeOf(float requestedDamage, float requestedImpulse) => 0f;

    /// <summary>Throws <see cref="ResourceConfigurationException"/> when a value would make the formulas meaningless.</summary>
    public void Validate()
    {
    }
}
