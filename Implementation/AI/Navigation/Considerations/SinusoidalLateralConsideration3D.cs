namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Jmodot.Implementation.AI.BB;
using Shared;

/// <summary>
/// A steering consideration that produces a lateral weave: an internally-clocked sine off the
/// agent's own heading pushes interest alternately to either side, producing a serpentine gait
/// with no waypoints and no target. A per-agent, seed-derived phase offset (held on the
/// processor-owned runtime) keeps agents sharing one .tres from weaving in lockstep. The
/// inherited <see cref="BaseAIConsideration3D.Weight"/> IS the wave's amplitude.
/// </summary>
[GlobalClass, Tool]
public partial class SinusoidalLateralConsideration3D : BaseAIConsideration3D
{
    private const float MinPeriod = 0.0001f;

    /// <summary>
    /// Seconds per full wave. A feel constant, not a strength axis. Non-positive values would
    /// divide the phase by zero, so they are clamped at read and warn once per instance.
    /// </summary>
    [Export] public float Period { get; private set; } = 2.5f;

    /// <summary>Latch so a non-positive Period warns once per instance, not every frame.</summary>
    private bool _periodViolationLogged;

    /// <summary>Per-agent gait state: the desync offset plus this agent's own time accumulator.</summary>
    internal sealed class SineRuntime : AIConsiderationRuntime
    {
        public float Offset;
        public float AccumulatedTime;
    }

    public override AIConsiderationRuntime CreateRuntime(IBlackboard? blackboard)
    {
        int entitySeed = 0;
        bool hasSeed = blackboard != null && blackboard.TryGet<int>(BBDataSig.EntitySeed, out entitySeed);
        if (!hasSeed)
        {
            JmoLogger.Warning(this,
                "[Lineage] SinusoidalLateralConsideration3D: no EntitySeed — phase offset 0 (unseeded).");
        }

        return new SineRuntime { Offset = hasSeed ? DeriveOffset(entitySeed) : 0f, AccumulatedTime = 0f };
    }

    protected override Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions,
        SteeringDecisionContext3D context3D,
        IBlackboard blackboard,
        AIConsiderationRuntime? runtime)
    {
        var scores = directions.Directions.ToDictionary(dir => dir, _ => 0f);

        // A missing runtime means this consideration was evaluated outside a processor: sample the
        // unseeded origin rather than parking per-agent state on the shared Resource or allocating a
        // throwaway on a path that can run every frame.
        var sine = NarrowRuntime<SineRuntime>(runtime);
        float time = 0f;
        if (sine != null)
        {
            sine.AccumulatedTime += context3D.PhysicsDelta;
            time = sine.AccumulatedTime + sine.Offset;
        }

        float phase = Mathf.Sin(Mathf.Tau * time / ResolvePeriod());
        Vector3 lateral = Vector3.Up.Cross(context3D.AgentFacingDirection);

        foreach (var dir in directions.Directions)
        {
            scores[dir] = SinusoidalLateralScoring.Score(dir, lateral, phase);
        }

        return scores;
    }

    private float ResolvePeriod()
    {
        if (Period > 0f)
        {
            return Period;
        }

        if (!_periodViolationLogged)
        {
            JmoLogger.Warning(this,
                $"Consideration '{ResourceName}' has a non-positive Period ({Period}); clamping to {MinPeriod}. " +
                "Further violations from this instance are suppressed.");
            _periodViolationLogged = true;
        }

        return MinPeriod;
    }

    // Deterministic per-agent phase offset in [0, 1000), folded straight from the seed — no
    // JmoRng construction (keeps this off the SIGSEGV-prone ctor and avoids a per-frame alloc).
    private static float DeriveOffset(int entitySeed)
    {
        int derived = SeedManager.DeriveChild(entitySeed, SeedKinds.SineLateral);
        return (uint)derived % 1_000_000u / 1000f;
    }

    #region Test Helpers
#if TOOLS
    internal void SetPeriodForTest(float period) => Period = period;
    internal static SineRuntime RuntimeWithOffsetForTest(float offset)
        => new() { Offset = offset, AccumulatedTime = 0f };
#endif
    #endregion
}
