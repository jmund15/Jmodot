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
/// A per-agent, seed-derived time offset (held on the processor-owned runtime) prevents
/// synchronized wandering across critters sharing the same .tres resource.
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

    #endregion

    /// <summary>Per-agent wander state: the desync offset plus this agent's own time accumulator.</summary>
    internal sealed class WanderRuntime : AIConsiderationRuntime
    {
        public float Offset;
        public float AccumulatedTime;
    }

    public override void Initialize(DirectionSet3D directions)
    {
        base.Initialize(directions);

        if (_noise == null)
        {
            JmoLogger.Warning(this, "No FastNoiseLite noise configured — wander direction will be constant.");
        }
    }

    public override AIConsiderationRuntime CreateRuntime(IBlackboard? blackboard)
    {
        int entitySeed = 0;
        bool hasSeed = blackboard != null && blackboard.TryGet<int>(BBDataSig.EntitySeed, out entitySeed);
        if (!hasSeed)
        {
            JmoLogger.Warning(this, "[Lineage] WanderConsideration3D: no EntitySeed — desync offset 0 (unseeded).");
        }

        return new WanderRuntime { Offset = hasSeed ? DeriveOffset(entitySeed) : 0f, AccumulatedTime = 0f };
    }

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard,
        AIConsiderationRuntime? runtime)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        // A missing runtime means this consideration was evaluated outside a processor: sample the
        // unseeded origin rather than parking per-agent state on the shared Resource. A throwaway
        // runtime would sample a constant anyway (a fresh accumulator never advances past one tick)
        // while churning the heap on a path that can run every frame.
        var wander = NarrowRuntime<WanderRuntime>(runtime);
        float time = 0f;
        if (wander != null)
        {
            wander.AccumulatedTime += (float)(1.0 / Engine.PhysicsTicksPerSecond);
            time = wander.AccumulatedTime + wander.Offset;
        }
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
                scores[dir] = alignment;
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

    // Deterministic per-agent desync offset in [0, 1000), folded straight from the seed — no
    // JmoRng construction (keeps this off the SIGSEGV-prone ctor and avoids a per-frame alloc).
    private static float DeriveOffset(int entitySeed)
    {
        int derived = SeedManager.DeriveChild(entitySeed, SeedKinds.Wander);
        return (uint)derived % 1_000_000u / 1000f;
    }

    #region Test Helpers
#if TOOLS
    internal void SetNoise(FastNoiseLite? noise) => _noise = noise;
    internal static WanderRuntime RuntimeWithOffsetForTest(float offset)
        => new() { Offset = offset, AccumulatedTime = 0f };
#endif
    #endregion
}
