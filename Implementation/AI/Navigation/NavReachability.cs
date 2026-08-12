namespace Jmodot.Implementation.AI.Navigation;

using Godot;

/// <summary>
/// Decides whether a requested nav target is close enough to the navigation mesh to path to, as two
/// independent components rather than one scalar distance.
/// </summary>
/// <remarks>
/// A navigation mesh describes the traversable area for an agent's CENTRE position, and closest-point
/// queries return points ON that mesh — so a mesh point sits roughly half an agent above the surface
/// the agent's feet occupy. Callers hand in world targets at feet height. Under a single scalar
/// distance that structural offset spends part of the LATERAL budget, which is the only thing
/// "reachable" was ever meant to express: a target standing dead-centre on flat, walkable floor
/// already measures a large fraction of the tolerance before any real unreachability exists. Splitting
/// the axes lets the lateral band mean what it says and gives the vertical band a stated derivation.
/// </remarks>
public static class NavReachability
{
    /// <summary>How far LATERALLY a target may sit from the mesh and still be considered reachable.</summary>
    public const float PlanarToleranceMeters = 1f;

    /// <summary>
    /// Half the agent height the mesh is baked for — the mesh surface tracks agent centres, so a
    /// feet-height target is structurally this far below the mesh point that answers it. Sized from
    /// Godot's default <c>NavigationMesh.AgentHeight</c> of 1.5 m so shorter agents are covered too.
    /// </summary>
    private const float HalfAgentHeightMeters = 0.75f;

    /// <summary>
    /// Vertical bake quantization: the mesh surface is snapped to <c>NavigationMesh.CellHeight</c>
    /// (default 0.25 m), so a mesh point may sit up to one cell off the geometry it was parsed from.
    /// </summary>
    private const float CellHeightQuantizationMeters = 0.25f;

    /// <summary>Slack for sub-cell geometry variation (ramps, tile seams) that neither term above covers.</summary>
    private const float VerticalSlackMeters = 0.25f;

    /// <summary>
    /// Vertical band tolerated between a feet-height target and the centre-height mesh point answering
    /// it. Anything past this is a genuine height discontinuity — a different floor, a cliff, a pit.
    /// </summary>
    public const float VerticalToleranceMeters =
        HalfAgentHeightMeters + CellHeightQuantizationMeters + VerticalSlackMeters;

    /// <summary>
    /// True when <paramref name="closestNavPoint"/> is within the lateral and vertical bands of
    /// <paramref name="candidate"/>. Both deltas are reported so callers can log the axis that failed.
    /// </summary>
    public static bool IsReachable(
        Vector3 candidate, Vector3 closestNavPoint, out float planarDelta, out float verticalDelta)
    {
        Vector3 delta = closestNavPoint - candidate;
        planarDelta = new Vector2(delta.X, delta.Z).Length();
        verticalDelta = Mathf.Abs(delta.Y);
        return planarDelta <= PlanarToleranceMeters && verticalDelta <= VerticalToleranceMeters;
    }
}
