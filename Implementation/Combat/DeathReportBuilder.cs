namespace Jmodot.Implementation.Combat;

using System.Collections.Generic;
using Core.Combat;
using Core.Combat.Reactions;
using Core.Health;
using Core.Identification;

/// <summary>
/// Turns a death into a <see cref="DeathReport"/>, pairing it with the hit that caused it.
/// </summary>
public static class DeathReportBuilder
{
    /// <summary>
    /// Builds the report for <paramref name="victim"/> dying. <paramref name="pairedHit"/> is the last hit report
    /// the victim published, or null; it pairs when it landed <paramref name="framesSinceHit"/> physics frames ago
    /// or fewer, up to <see cref="ImpactWeightProfile.DeathPairWindowFrames"/> inclusive.
    /// </summary>
    public static DeathReport Build(
        ImpactWeightProfile profile, Node victim, Vector3 victimPoint, IReadOnlyList<Category> categories,
        Node? killer, DamageKind kind, float overkill, HitReport? pairedHit, int framesSinceHit)
    {
        bool paired = pairedHit != null && framesSinceHit >= 0 && framesSinceHit <= profile.DeathPairWindowFrames;

        return new DeathReport
        {
            Target = victim,
            Source = paired ? pairedHit!.Source : null,
            Tags = paired ? pairedHit!.Tags : [],
            Point = victimPoint,
            Categories = categories,
            Killer = ReferenceEquals(killer, victim) ? null : killer,
            Kind = kind,
            Overkill = overkill,
            Direction = paired ? pairedHit!.Direction : Vector3.Zero,
            Magnitude = paired ? pairedHit!.Magnitude : profile.DefaultDeathMagnitude,
            Paired = paired,
        };
    }
}
