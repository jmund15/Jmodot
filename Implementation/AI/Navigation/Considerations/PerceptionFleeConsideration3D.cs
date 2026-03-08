namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Identification;
using Core.Movement;
using Core.Shared.Attributes;

/// <summary>
/// A steering consideration that computes confidence-weighted flee direction from
/// AIPerceptionManager3D memories. Unlike MultiFleeConsideration3D (BB-based, all
/// threats equal weight), each threat's contribution is scaled by its CurrentConfidence.
///
/// When a wizard leaves the sensor zone, their memory decays (1.0 → 0.0 over ForgetTime).
/// This produces a gradually weakening flee force instead of the instant-stop behavior
/// caused by clearing BB.ThreatPositions on zone exit.
///
/// Cornered emergence: when two equal-confidence threats are on opposing sides,
/// their weighted flee vectors cancel → zero scores → CorneredDetector triggers.
/// </summary>
[GlobalClass, Tool]
public partial class PerceptionFleeConsideration3D : BaseAIConsideration3D
{
    [ExportGroup("Threat Source")]
    [Export, RequiredExport] private Category _threatCategory = null!;

    [ExportGroup("Steering Behavior")]
    [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
    private float _fleeWeight = 1.5f;

    private readonly Dictionary<Vector3, float> _cachedScores = new();

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard)
    {
        _cachedScores.Clear();
        foreach (var dir in directions.Directions)
        {
            _cachedScores[dir] = 0f;
        }
        var scores = _cachedScores;
        if (context3D.Memory == null) { return scores; }

        var threats = context3D.Memory.GetSensedByCategory(_threatCategory);
        var combinedFlee = Vector3.Zero;

        foreach (var threat in threats)
        {
            var toThreat = threat.LastKnownPosition - context3D.AgentPosition;
            var flat = new Vector3(toThreat.X, 0, toThreat.Z);
            if (flat.LengthSquared() < 0.0001f) { continue; }

            combinedFlee -= flat.Normalized() * threat.CurrentConfidence;
        }

        if (combinedFlee.LengthSquared() < 0.0001f) { return scores; }

        // Preserve magnitude — it encodes aggregate confidence urgency
        float magnitude = combinedFlee.Length();
        var fleeDir = combinedFlee / magnitude;

        foreach (var dir in directions.Directions)
        {
            var flatDir = new Vector3(dir.X, 0, dir.Z);
            if (flatDir.LengthSquared() < 0.001f) { continue; }
            flatDir = flatDir.Normalized();
            float alignment = flatDir.Dot(fleeDir);
            if (alignment > 0f)
            {
                scores[dir] = alignment * magnitude * _fleeWeight;
            }
        }

        return scores;
    }

    #region Test Helpers
#if TOOLS
    internal void SetThreatCategory(Category category) => _threatCategory = category;
    internal void SetFleeWeight(float value) => _fleeWeight = value;
#endif
    #endregion
}
