namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.Combat;
using Core.Combat.Reactions;
using Core.Identification;
using Godot;
using Implementation.Actors;
using Implementation.Shared;

/// <summary>
/// Fires when a wall-shaped <see cref="ImpactResult"/> with sufficient perpendicular
/// impact speed has been logged within <see cref="LookbackSeconds"/>. Replaces the
/// CapturedState-owned event-subscription routing — the HSM observes; physics drives.
/// </summary>
/// <remarks>
/// <para>
/// Stateless predicate over the queryable CombatLog channel. The condition holds NO
/// non-[Export] private state — same .tres can be shared across actors via
/// [ext_resource] without leaking state between them. Per
/// <c>TransitionCondition_Stateless_Rule</c> (Memory MCP) — a prior 2026-05-10
/// attempt at this class name introduced a `_wallImpactPending` latch and `_boundDetector`
/// subscription; both are explicitly forbidden by the framework contract.
/// </para>
/// <para>
/// Wall geometry: <c>|Normal · Vector3.Up| &lt; WallNormalThreshold</c> (lower threshold =
/// stricter wall). Floor (normal ≈ up) and ceiling (normal ≈ down) are rejected;
/// horizontal walls pass.
/// </para>
/// <para>
/// Approach angle: <see cref="MinSpeedAlongNormal"/> alone fuses angle with severity, so a fast
/// glancing scrape clears a bar a slow head-on hit fails. <see cref="MaxApproachAngleDegrees"/>
/// gates the geometry separately, off <see cref="ImpactInfo.ApproachDegrees"/>.
/// </para>
/// <para>
/// Category exclusion: when <see cref="ExcludeCategory"/> is set, contacts whose collider
/// is in that category (e.g., other characters body-checking the captured actor) are
/// skipped. A wave-captured wizard slamming into another character should stay Captured,
/// not transition to WallHit.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class WallImpactCondition : CombatLogCondition
{
    /// <summary>Surface-normal dot product threshold. Lower = stricter wall geometry. Default 0.3.</summary>
    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float WallNormalThreshold { get; private set; } = 0.3f;

    /// <summary>
    /// Minimum speed ALONG THE CONTACT NORMAL (m/s) required to fire — the perpendicular
    /// severity of the hit, not how fast the body was travelling. Below this, the contact is a
    /// gentle bump. Distinct from <see cref="ImpactDetector"/>'s own <c>MinImpactSpeed</c>, which
    /// gates the body's TOTAL speed upstream: a fast scrape can clear the detector and fail here.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,100,0.1")]
    public float MinSpeedAlongNormal { get; private set; } = 6f;

    /// <summary>Time window (seconds, combat-time) the condition scans back. Wide enough to absorb same-frame ordering between the detector tick and HSM tick; tight enough that stale impacts from prior captures don't re-fire on state re-entry.</summary>
    [Export(PropertyHint.Range, "0.0,2.0,0.05")]
    public float LookbackSeconds { get; private set; } = 0.1f;

    /// <summary>Widest approach angle that still counts, where 0° is dead-on and 90° a parallel graze. Reject glancing scrapes by lowering it; the default 90° cannot reject anything, so an instance that never authors it behaves as if there were no angle gate.</summary>
    [Export(PropertyHint.Range, "0,90,1,suffix:°")]
    public float MaxApproachAngleDegrees { get; private set; } = 90f;

    /// <summary>Optional category whose colliders should NOT trigger the transition. Typically the project's entity category, so body-checking other characters doesn't fire WallHit.</summary>
    [Export] public Category? ExcludeCategory { get; private set; }

    protected override bool CheckEvent(CombatLog log)
    {
        foreach (var r in log.GetAllCombatResultsWithinCombatTime<ImpactResult>(LookbackSeconds))
        {
            if (r.Info.SpeedAlongNormal < MinSpeedAlongNormal) { continue; }
            if (!r.Info.IsWall(WallNormalThreshold)) { continue; }
            if (r.Info.ApproachDegrees > MaxApproachAngleDegrees) { continue; }
            if (r.Info.Collider.HasCategory(ExcludeCategory)) { continue; }
            return true;
        }
        return false;
    }

    #region Test Helpers
#if TOOLS
    internal void SetTestExports(float wallNormalThreshold, float minSpeedAlongNormal, float lookbackSeconds, Category? excludeCategory, float maxApproachAngleDegrees)
    {
        WallNormalThreshold = wallNormalThreshold;
        MinSpeedAlongNormal = minSpeedAlongNormal;
        LookbackSeconds = lookbackSeconds;
        ExcludeCategory = excludeCategory;
        MaxApproachAngleDegrees = maxApproachAngleDegrees;
    }
#endif
    #endregion
}
