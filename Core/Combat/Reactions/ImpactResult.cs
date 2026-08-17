namespace Jmodot.Core.Combat.Reactions;

using System;
using Godot;
using Implementation.Actors;

/// <summary>
/// Result type produced by <see cref="Jmodot.Implementation.Actors.ImpactDetector"/> on each rising-edge slide-collision contact
/// whose pre-move velocity exceeded the detector's MinImpactSpeed gate. Carries the raw
/// geometric facts of the contact for HSM-side queries (WallImpactCondition, etc.) without
/// requiring HSM consumers to subscribe to the detector's transient event stream.
/// </summary>
/// <remarks>
/// <para>
/// <c>Info</c> is the same <see cref="ImpactInfo"/> the detector publishes on its event channel,
/// so both channels describe one contact with one value — the log channel cannot grow a fact its
/// event twin lacks. Geometry (<c>IsWall</c>, <c>IsCeiling</c>) and identity (<c>Collider</c>)
/// are queried through it.
/// </para>
/// <para>
/// <c>ApproachSpeed</c> is the body's TOTAL pre-collision speed, which
/// <see cref="ImpactInfo.SpeedAlongNormal"/> is the perpendicular component of. The pair is what
/// separates angle from severity: a fast glancing scrape and a slow head-on hit are
/// indistinguishable from <c>SpeedAlongNormal</c> alone.
/// </para>
/// </remarks>
public sealed record ImpactResult(ImpactInfo Info, float ApproachSpeed) : CombatResult
{
    /// <summary>
    /// Angle between the body's travel and the contact normal, in degrees: 0° is dead-on, 90° a
    /// parallel graze. Never NaN — an absent approach speed reports 90° and an over-unity ratio
    /// saturates at 0°, so a consumer may compare it without guarding either case.
    /// </summary>
    /// <remarks>
    /// The zero-approach-speed answer is 90°, not 0° and not "skip the gate": a missing approach
    /// speed must fail closed on the graze side. Reading it as a perfect hit would let every
    /// contact through the widest gate an author could set.
    /// </remarks>
    public float ApproachDegrees => ApproachSpeed <= 0f
        ? 90f
        : Mathf.RadToDeg(MathF.Acos(Math.Clamp(Info.SpeedAlongNormal / ApproachSpeed, 0f, 1f)));
}
