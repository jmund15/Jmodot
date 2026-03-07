namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Shared;

/// <summary>
/// A steering consideration that uses FastNoiseLite to generate time-varying
/// directional interest on the XZ plane. Creates organic meandering behavior
/// without waypoints or targets.
/// Per-instance random time offset prevents synchronized wandering across
/// critters sharing the same .tres resource.
/// </summary>
[GlobalClass, Tool]
public partial class WanderConsideration3D : BaseAIConsideration3D
{
    #region Exported Parameters

    [ExportGroup("Noise Configuration")]

    /// <summary>
    /// The noise resource driving direction variation. Controls temporal evolution
    /// speed via its Frequency property: low frequency = lazy drift,
    /// high frequency = jittery exploration.
    /// </summary>
    [Export]
    private FastNoiseLite? _noise;

    [ExportGroup("Steering Behavior")]

    /// <summary>
    /// Base weight of the wander consideration. Determines how strongly wander
    /// competes with other considerations (wall avoidance, flee, formation).
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
    private float _wanderWeight = 0.8f;

    #endregion

    /// <summary>
    /// Per-instance time offset to desync critters sharing the same noise resource.
    /// </summary>
    private float _timeOffset;

    /// <summary>
    /// Accumulated time from physics frames. Pause-aware — only advances when
    /// physics process is running, unlike wall-clock time.
    /// </summary>
    private float _accumulatedTime;

    public override void Initialize(DirectionSet3D directions)
    {
        base.Initialize(directions);
        _timeOffset = JmoRng.GetRndInRange(0f, 1000f);
        _accumulatedTime = 0f;

        if (_noise == null)
        {
            JmoLogger.Warning(this, "No FastNoiseLite noise configured — wander direction will be constant.");
        }
    }

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        // Accumulate time from physics frames (pause-aware, deterministic)
        _accumulatedTime += (float)(1.0 / Engine.PhysicsTicksPerSecond);
        float time = _accumulatedTime + _timeOffset;
        float noiseValue = _noise?.GetNoise1D(time) ?? 0f;

        Vector3 wanderDirection = CalculateAngularDirection(noiseValue);

        // Score each direction by alignment with wander direction
        foreach (var dir in directions.Directions)
        {
            Vector3 flatDir = new Vector3(dir.X, 0, dir.Z);
            if (flatDir.LengthSquared() < 0.001f)
            {
                continue;
            }

            flatDir = flatDir.Normalized();
            float alignment = flatDir.Dot(wanderDirection);

            if (alignment > 0)
            {
                scores[dir] = alignment * _wanderWeight;
            }
        }

        return scores;
    }

    /// <summary>
    /// Converts a single noise value in [-1, 1] to a unit direction on the XZ plane.
    /// Maps to two full rotations (Tau): [-1,1] → [0, 4π]. This ensures the practical
    /// noise output range (~[-0.7, 0.7] for Simplex) still sweeps the complete circle,
    /// giving uniform quadrant coverage regardless of noise distribution clustering near 0.
    /// </summary>
    public static Vector3 CalculateAngularDirection(float noiseValue)
    {
        float angle = (noiseValue + 1f) * Mathf.Tau;
        return new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
    }

    #region Test Helpers
#if TOOLS
    internal void SetWanderWeight(float value) => _wanderWeight = value;
    internal void SetNoise(FastNoiseLite? noise) => _noise = noise;
    internal void SetTimeOffset(float offset) => _timeOffset = offset;
#endif
    #endregion
}
