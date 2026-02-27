namespace Jmodot.Core.AI.Navigation.Zones;

using Godot;

/// <summary>
/// Abstract base for zone shape definitions used by ZoneBoundaryConsideration3D.
/// Subclasses define containment geometry (sphere, box, polygon, etc.) and provide
/// normalized distance and direction-to-interior calculations.
///
/// Follows the Resource Strategy Hierarchy pattern: abstract [GlobalClass] base
/// with concrete subclasses saved as .tres or SubResources.
/// </summary>
[GlobalClass, Tool]
public abstract partial class ZoneShape3D : Resource
{
    /// <summary>
    /// Returns the agent's normalized distance from the zone boundary.
    /// 0 = at center / deepest interior, 1 = at boundary edge, >1 = outside zone.
    /// Distances are calculated on the XZ plane (Y ignored for ground-based containment).
    /// </summary>
    public abstract float GetNormalizedDistance(Vector3 agentPosition, Vector3 zoneCenter);

    /// <summary>
    /// Returns a normalized direction vector pointing from the agent toward
    /// the zone interior. Used for penalty scoring — directions aligned with
    /// this vector are unpenalized, directions opposing it are penalized.
    /// Must return a valid (non-zero) normalized vector even for degenerate cases.
    /// </summary>
    public abstract Vector3 GetDirectionToInterior(Vector3 agentPosition, Vector3 zoneCenter);

    /// <summary>
    /// Returns a random point within the zone interior, relative to <paramref name="center"/>.
    /// Used by nav-aware wander to generate navigation target points.
    /// Distribution should be approximately uniform across the zone area.
    /// Points are on the XZ plane (Y matches center Y).
    /// </summary>
    public abstract Vector3 SampleRandomInteriorPoint(Vector3 center);
}
