namespace Jmodot.Implementation.AI.Navigation.SteeringModifiers;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.SteeringModifiers;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// A steering modifier that scales directional scores based on the agent's distance
/// to the current navigation target. Uses a designer-authored <see cref="Curve"/> to
/// map normalized distance (0 = at target, 1 = at max distance) to a score multiplier.
///
/// <para><b>Common use cases:</b></para>
/// <list type="bullet">
///   <item>Urgency ramping: inverse curve makes close targets override distractions</item>
///   <item>Diminishing returns: S-curve prevents path-following from dominating at range</item>
///   <item>Distance gating: step curve ignores targets beyond a threshold</item>
/// </list>
/// </summary>
/// <remarks>
///     <para><b>Context Dependency:</b> Requires non-zero
///     <see cref="SteeringDecisionContext3D.TargetPosition"/>. When TargetPosition is zero
///     (no nav target set), the modifier early-returns without modifying scores.</para>
///
///     <para><b>Compatible Considerations:</b> NavigationPath3DConsideration, FleeConsideration3D,
///     MultiFleeConsideration3D, FormationConsideration3D, StatDrivenConsideration3D — any
///     consideration where the agent has an active navigation target.</para>
///
///     <para><b>No-op Combinations:</b> WanderConsideration3D (no target → early return, zero
///     effect). ZoneBoundaryConsideration3D (already distance-based internally — applying
///     distance scaling adds no value).</para>
/// </remarks>
[GlobalClass]
public partial class DistanceScalingModifier3D : SteeringConsiderationModifier3D
{
    [Export(PropertyHint.Range, "1.0, 50.0, 0.5")]
    private float _maxDistance = 20.0f;

    [Export, RequiredExport]
    private Curve _distanceCurve = null!;

    public override void Modify(ref Dictionary<Vector3, float> scores,
        SteeringDecisionContext3D context, IBlackboard blackboard)
    {
        if (context.TargetPosition.IsZeroApprox()) { return; }

        float dist = context.AgentPosition.DistanceTo(context.TargetPosition);
        float normalized = Mathf.Clamp(dist / _maxDistance, 0f, 1f);
        float multiplier = _distanceCurve.SampleBaked(normalized);

        foreach (var key in scores.Keys.ToList())
        {
            scores[key] *= multiplier;
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetMaxDistance(float maxDistance) => _maxDistance = maxDistance;
    internal void SetDistanceCurve(Curve curve) => _distanceCurve = curve;
#endif
    #endregion
}
