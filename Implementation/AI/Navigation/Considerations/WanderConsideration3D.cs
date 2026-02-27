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
///
/// Checks a BB bool flag (configurable key) to enable/disable scoring.
/// When disabled, returns zero scores — allowing a BT to toggle move/idle pacing.
///
/// Per-instance random time offset prevents synchronized wandering across
/// critters sharing the same .tres resource.
/// </summary>
[GlobalClass]
public partial class WanderConsideration3D : BaseAIConsideration3D
{
    /// <summary>
    /// Default BB key used when no custom active key is configured.
    /// </summary>
    public static readonly StringName DefaultActiveKey = new("WanderActive");

    #region Exported Parameters

    [ExportGroup("Noise Configuration")]

    /// <summary>
    /// The noise resource driving direction variation. Different configurations
    /// produce different movement personalities: low frequency = lazy drift,
    /// high frequency = jittery exploration.
    /// </summary>
    [Export]
    private FastNoiseLite? _noise;

    /// <summary>
    /// How fast the noise samples evolve over time.
    /// Low values (0.1-0.3) = slow, lazy direction changes.
    /// High values (1.0-3.0) = rapid, energetic direction changes.
    /// </summary>
    [Export(PropertyHint.Range, "0.05, 5.0, 0.05")]
    private float _noiseTimeScale = 0.5f;

    [ExportGroup("Steering Behavior")]

    /// <summary>
    /// Base weight of the wander consideration. Determines how strongly wander
    /// competes with other considerations (wall avoidance, flee, formation).
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
    private float _wanderWeight = 0.8f;

    /// <summary>
    /// BB key for the bool flag that enables/disables wandering.
    /// When the flag is false or missing, returns zero scores.
    /// </summary>
    [Export]
    private StringName? _activeKey;

    [ExportGroup("Score Propagation")]

    [Export]
    private bool _propagateScores = true;

    [Export(PropertyHint.Range, "1, 4, 1")]
    private int _dirsToPropagate = 2;

    [Export(PropertyHint.Range, "0.1, 0.9, 0.05")]
    private float _propDiminishWeight = 0.5f;

    #endregion

    private List<Vector3> _orderedDirections = null!;

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
        _orderedDirections = directions.Directions.ToList();
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

        // Check active flag — conservative default: inactive when missing
        var key = _activeKey ?? DefaultActiveKey;
        if (!blackboard.TryGet<bool>(key, out var active) || !active)
        {
            return scores;
        }

        // Accumulate time from physics frames (pause-aware, deterministic)
        _accumulatedTime += (float)(1.0 / Engine.PhysicsTicksPerSecond);
        float time = _accumulatedTime * _noiseTimeScale + _timeOffset;
        float noiseX = _noise?.GetNoise1D(time) ?? 0f;
        float noiseZ = _noise?.GetNoise1D(time + 1000f) ?? 0f;

        Vector3 wanderDirection = CalculateNoiseDirection(noiseX, noiseZ);

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

        // Propagate scores to neighbors
        if (_propagateScores)
        {
            SteeringPropagation.PropagateScores(scores, _orderedDirections, _dirsToPropagate, _propDiminishWeight);
        }

        return scores;
    }

    /// <summary>
    /// Calculates a normalized XZ-plane direction from two noise channel values.
    /// Uses the noise values as X and Z components of a direction vector.
    /// Returns Vector3.Forward as fallback when both inputs are near zero.
    /// </summary>
    public static Vector3 CalculateNoiseDirection(float noiseX, float noiseZ)
    {
        var dir = new Vector3(noiseX, 0, noiseZ);
        return dir.LengthSquared() < 0.0001f ? Vector3.Forward : dir.Normalized();
    }

    #region Test Helpers
#if TOOLS
    internal void SetWanderWeight(float value) => _wanderWeight = value;
    internal void SetNoiseTimeScale(float value) => _noiseTimeScale = value;
    internal void SetNoise(FastNoiseLite? noise) => _noise = noise;
    internal void SetActiveKey(StringName? key) => _activeKey = key;
    internal void SetTimeOffset(float offset) => _timeOffset = offset;
    internal void SetPropagateScores(bool value) => _propagateScores = value;
#endif
    #endregion
}
