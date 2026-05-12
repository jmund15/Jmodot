namespace Jmodot.Core.Combat.Reactions;

using Godot;

/// <summary>
/// Result type produced by <c>ImpactDetector</c> on each rising-edge slide-collision contact
/// whose pre-move velocity exceeded the detector's MinImpactSpeed gate. Carries the raw
/// geometric facts of the contact for HSM-side queries (WallImpactCondition, etc.) without
/// requiring HSM consumers to subscribe to the detector's transient event stream.
/// </summary>
/// <remarks>
/// <para>
/// <c>SpeedAlongNormal</c> is the perpendicular-component impact severity, clamped to ≥0
/// — NOT the body's total kinetic magnitude. A grunt sliding horizontally on a floor
/// reports near-zero SpeedAlongNormal for the floor contact (velocity perpendicular to
/// floor normal). See <c>ImpactInfo.ComputeSpeedAlongNormal</c> for the math.
/// </para>
/// <para>
/// <c>Normal</c> follows Godot convention: points AWAY from the surface, out toward the
/// colliding body. Consumers query surface kind (wall/floor/ceiling) via dot-product math
/// against <c>Vector3.Up</c>.
/// </para>
/// </remarks>
public sealed record ImpactResult(
    Node3D Collider,
    Vector3 Normal,
    float SpeedAlongNormal) : CombatResult;
