namespace Jmodot.Core.Combat;

using System;
using System.Collections.Generic;
using System.Linq;
using Implementation.Shared.GodotExceptions;

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
    public float MagnitudeOf(float requestedDamage, float requestedImpulse) =>
        Math.Max(HarmOf(requestedDamage), Fraction(requestedImpulse, ReferenceImpulse));

    /// <summary>The damage axis of <see cref="MagnitudeOf"/>: requested damage over <see cref="ReferenceDamage"/>, in [0, 1].</summary>
    public float HarmOf(float requestedDamage) => Fraction(requestedDamage, ReferenceDamage);

    /// <summary><paramref name="amount"/> over <paramref name="reference"/> clamped to [0, 1]; 0 when either is unusable.</summary>
    public static float Fraction(float amount, float reference)
    {
        if (!(amount > 0f) || !(reference > 0f) || !float.IsFinite(amount) || !float.IsFinite(reference)) { return 0f; }
        return Math.Min(1f, amount / reference);
    }

    /// <summary>The largest floor among <paramref name="tags"/>, or 0 when none carries one.</summary>
    public float FloorFor(IEnumerable<CombatTag> tags)
    {
        float floor = 0f;
        foreach (var tagFloor in TagFloors)
        {
            if (tagFloor?.Tag == null || tagFloor.Floor <= floor) { continue; }
            if (tags.Any(t => t?.TagId == tagFloor.Tag.TagId)) { floor = tagFloor.Floor; }
        }

        return floor;
    }

    /// <summary>Throws <see cref="ResourceConfigurationException"/> when a value would make the formulas meaningless.</summary>
    public void Validate()
    {
        RequirePositive(ReferenceDamage, nameof(ReferenceDamage));
        RequirePositive(ReferenceImpulse, nameof(ReferenceImpulse));
        RequirePositive(ReferenceDeltaV, nameof(ReferenceDeltaV));
        if (!(CritMultiplier >= 1f) || !float.IsFinite(CritMultiplier))
        {
            throw new ResourceConfigurationException($"{nameof(CritMultiplier)} must be a finite value of at least 1 (was {CritMultiplier}).", this);
        }

        if (!(DefaultDeathMagnitude >= 0f && DefaultDeathMagnitude <= 1f))
        {
            throw new ResourceConfigurationException($"{nameof(DefaultDeathMagnitude)} must be in [0, 1] (was {DefaultDeathMagnitude}).", this);
        }

        if (DeathPairWindowFrames < 0)
        {
            throw new ResourceConfigurationException($"{nameof(DeathPairWindowFrames)} must not be negative (was {DeathPairWindowFrames}).", this);
        }

        var seen = new HashSet<StringName>();
        foreach (var tagFloor in TagFloors)
        {
            if (tagFloor?.Tag == null)
            {
                throw new ResourceConfigurationException($"{nameof(TagFloors)} holds an entry with no tag.", this);
            }

            if (!(tagFloor.Floor >= 0f && tagFloor.Floor <= 1f))
            {
                throw new ResourceConfigurationException($"{nameof(TagFloors)} floor for '{tagFloor.Tag.TagId}' must be in [0, 1] (was {tagFloor.Floor}).", this);
            }

            if (!seen.Add(tagFloor.Tag.TagId))
            {
                throw new ResourceConfigurationException($"{nameof(TagFloors)} lists '{tagFloor.Tag.TagId}' twice.", this);
            }
        }
    }

    private void RequirePositive(float value, string name)
    {
        if (!(value > 0f) || !float.IsFinite(value))
        {
            throw new ResourceConfigurationException($"{name} must be a finite positive value (was {value}).", this);
        }
    }
}
