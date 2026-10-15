namespace Jmodot.Implementation.Combat;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Combat;
using Core.Combat.Reactions;
using Core.Health;
using Core.Identification;

/// <summary>
/// Everything <see cref="HitReportBuilder.FromPayload"/> needs, resolved by the caller: the builder reads
/// no node, so a pure test supplies plain values.
/// </summary>
public readonly record struct PayloadHitInputs(
    Node Victim, Node? Attacker, Node? Source, Vector3 Point, Vector3 Direction,
    float HealthBefore, float HealthAfter, float VictimMaxHealth, bool Indestructible, bool Died,
    IReadOnlyList<DamageResult> Damage, float RequestedImpulse, float AppliedDeltaV,
    IReadOnlyList<Category> Categories, bool Sustained, float EffectivenessScale = 1f);

/// <summary>
/// Everything <see cref="HitReportBuilder.FromImpact"/> needs: a body that struck a surface hard enough to be hurt.
/// </summary>
public readonly record struct ImpactHitInputs(
    Node Victim, Node? Source, Vector3 Point, Vector3 Normal, float SpeedAlongNormal,
    float HealthBefore, float HealthAfter, float VictimMaxHealth, bool Died, float RequestedDamage,
    IReadOnlyList<Category> Categories);

/// <summary>
/// Turns a resolved hit into a <see cref="HitReport"/>: the one place hit intensity is computed.
/// </summary>
public static class HitReportBuilder
{
    /// <summary>Builds the report for a payload that struck a health component.</summary>
    public static HitReport? FromPayload(ImpactWeightProfile profile, in PayloadHitInputs i)
    {
        float damage = i.Indestructible
            ? i.Damage.Sum(d => d.FinalAmount)
            : Math.Max(0f, i.HealthBefore - i.HealthAfter);
        if (!(damage > 0f) && !(i.AppliedDeltaV > 0f)) { return null; }

        float requestedDamage = i.Damage.Sum(d => d.OriginalAmount);
        float scaledRequest = i.Damage.Sum(d => d.FinalAmount);
        bool critical = i.Damage.Any(d => d.IsCritical);
        var tags = i.Damage.SelectMany(d => d.Tags ?? []).Distinct().ToList();

        var (weight, magnitude, damageFraction, pushFraction) = Derive(
            profile, damage, i.VictimMaxHealth, i.AppliedDeltaV, requestedDamage, i.RequestedImpulse, critical, tags);

        return new HitReport
        {
            Source = i.Source,
            Target = i.Victim,
            Attacker = i.Attacker,
            Tags = tags,
            Sustained = i.Sustained,
            Point = i.Point,
            Direction = i.Direction,
            Categories = i.Categories,
            Damage = damage,
            Killed = i.Died,
            Overkill = i.Died ? Math.Max(0f, scaledRequest - i.HealthBefore) : 0f,
            AppliedDeltaV = i.AppliedDeltaV,
            IsCritical = critical,
            Weight = weight,
            Magnitude = magnitude,
            DamageFraction = damageFraction,
            PushFraction = pushFraction,
            Kind = DamageKind.Direct,
            EffectivenessScale = i.EffectivenessScale,
        };
    }

    /// <summary>Builds the report for a body hurt by striking a surface; null when it lost no health.</summary>
    public static HitReport? FromImpact(ImpactWeightProfile profile, in ImpactHitInputs i)
    {
        float damage = Math.Max(0f, i.HealthBefore - i.HealthAfter);
        if (!(damage > 0f)) { return null; }

        var (weight, magnitude, damageFraction, pushFraction) = Derive(
            profile, damage, i.VictimMaxHealth, i.SpeedAlongNormal, i.RequestedDamage, 0f, false, []);

        return new HitReport
        {
            Source = i.Source,
            Target = i.Victim,
            Attacker = i.Source,
            Tags = [],
            Point = i.Point,
            Direction = (-i.Normal).Normalized(),
            Categories = i.Categories,
            Damage = damage,
            Killed = i.Died,
            Overkill = i.Died ? Math.Max(0f, i.RequestedDamage - i.HealthBefore) : 0f,
            Weight = weight,
            Magnitude = magnitude,
            DamageFraction = damageFraction,
            PushFraction = pushFraction,
            Kind = DamageKind.Environmental,
        };
    }

    /// <summary>Builds the report for direct damage that reached a health component outside any payload; null on no health lost.</summary>
    public static HitReport? FromHealthChange(
        ImpactWeightProfile profile, in HealthChangeEventArgs args, Node victim, Node? attacker,
        Vector3 victimPoint, IReadOnlyList<Category> categories)
    {
        float damage = -args.HealthDelta;
        if (!(damage > 0f)) { return null; }

        var (weight, magnitude, damageFraction, pushFraction) = Derive(
            profile, damage, args.MaxHealth, 0f, damage, 0f, false, []);

        return new HitReport
        {
            Source = attacker,
            Target = victim,
            Attacker = attacker,
            Tags = [],
            Point = victimPoint,
            Direction = (args.ImpactDirection ?? Vector3.Zero).Normalized(),
            Categories = categories,
            Damage = damage,
            Killed = args.NewHealth <= 0f,
            Weight = weight,
            Magnitude = magnitude,
            DamageFraction = damageFraction,
            PushFraction = pushFraction,
            Kind = DamageKind.Direct,
            EffectivenessScale = args.IncomingMagnitudeScale,
        };
    }

    internal static (float Weight, float Magnitude, float DamageFraction, float PushFraction) Derive(
        ImpactWeightProfile profile, float damage, float victimMaxHealth, float appliedDeltaV,
        float requestedDamage, float requestedImpulse, bool isCritical, IEnumerable<CombatTag> tags)
    {
        float damageFraction = ImpactWeightProfile.Fraction(damage, victimMaxHealth);
        float pushFraction = ImpactWeightProfile.Fraction(appliedDeltaV, profile.ReferenceDeltaV);
        float weight = Math.Max(Math.Max(damageFraction, pushFraction), profile.FloorFor(tags));
        if (isCritical) { weight *= profile.CritMultiplier; }

        return (Math.Min(1f, weight), profile.MagnitudeOf(requestedDamage, requestedImpulse), damageFraction, pushFraction);
    }
}
