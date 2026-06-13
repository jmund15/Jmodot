namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Jmodot.Implementation.AI.BB;
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

    private sealed class WanderRuntime
    {
        public float Offset;
        public float AccumulatedTime;
    }

    /// <summary>
    /// Per-agent wander runtime (desync offset + time accumulator), keyed by entity seed.
    /// </summary>
    /// <remarks>
    /// DOCUMENTED EXCEPTION to arch_rule_resource_config_runtime_split (a shared .tres Resource
    /// normally holds zero per-consumer state). Keying by the unique entity seed defeats the
    /// rule's primary harm — cross-agent stomping — since each agent touches only its own entry.
    /// The steering architecture offers no per-agent init seam (the processor's Initialize
    /// carries no Blackboard), so a CreateRuntime split is not viable here. Residual: a bounded
    /// leak of one ~12-byte entry per distinct agent for the run's lifetime.
    /// </remarks>
    private readonly Dictionary<int, WanderRuntime> _runtimeByEntity = new();
    private bool _warnedNoSeed;

    public override void Initialize(DirectionSet3D directions)
    {
        base.Initialize(directions);

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

        // Per-agent runtime: deterministic offset (from entity seed) + per-agent accumulator.
        var runtime = ResolveRuntime(blackboard);
        runtime.AccumulatedTime += (float)(1.0 / Engine.PhysicsTicksPerSecond);
        float time = runtime.AccumulatedTime + runtime.Offset;
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

    private WanderRuntime ResolveRuntime(IBlackboard? blackboard)
    {
        int entitySeed = 0;
        bool hasSeed = blackboard != null && blackboard.TryGet<int>(BBDataSig.EntitySeed, out entitySeed);
        if (!hasSeed && !_warnedNoSeed)
        {
            JmoLogger.Warning(this, "[Lineage] WanderConsideration3D: no EntitySeed — desync offset 0 (unseeded).");
            _warnedNoSeed = true;
        }

        if (!_runtimeByEntity.TryGetValue(entitySeed, out var runtime))
        {
            runtime = new WanderRuntime { Offset = hasSeed ? DeriveOffset(entitySeed) : 0f, AccumulatedTime = 0f };
            _runtimeByEntity[entitySeed] = runtime;
        }
        return runtime;
    }

    // Deterministic per-agent desync offset in [0, 1000), folded straight from the seed — no
    // JmoRng construction (keeps this off the SIGSEGV-prone ctor and avoids a per-frame alloc).
    private static float DeriveOffset(int entitySeed)
    {
        int derived = SeedManager.DeriveChild(entitySeed, SeedKinds.Wander);
        return (uint)derived % 1_000_000u / 1000f;
    }

    #region Test Helpers
#if TOOLS
    internal void SetWanderWeight(float value) => _wanderWeight = value;
    internal void SetNoise(FastNoiseLite? noise) => _noise = noise;
    internal void SetOffsetForTest(int entitySeed, float offset)
        => _runtimeByEntity[entitySeed] = new WanderRuntime { Offset = offset, AccumulatedTime = 0f };
#endif
    #endregion
}
