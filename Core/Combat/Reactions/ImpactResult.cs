namespace Jmodot.Core.Combat.Reactions;

using Implementation.Actors;

/// <summary>
/// Result type produced by <see cref="Jmodot.Implementation.Actors.ImpactDetector"/> on each rising-edge slide-collision contact
/// whose pre-move velocity exceeded the detector's MinImpactSpeed gate. Carries the raw
/// geometric facts of the contact for HSM-side queries (WallImpactCondition, etc.) without
/// requiring HSM consumers to subscribe to the detector's transient event stream.
/// </summary>
/// <remarks>
/// A pure <see cref="CombatResult"/> adapter over <see cref="ImpactInfo"/> — it carries no fact
/// of its own, so the log channel cannot describe a contact differently from the event channel
/// the detector publishes alongside it. Severity (<c>SpeedAlongNormal</c>, <c>ApproachSpeed</c>),
/// angle (<c>ApproachDegrees</c>), geometry (<c>IsWall</c>, <c>IsCeiling</c>) and identity
/// (<c>Collider</c>) are all queried through <see cref="Info"/>.
/// </remarks>
public sealed record ImpactResult(ImpactInfo Info) : CombatResult;
