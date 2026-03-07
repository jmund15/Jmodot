namespace Jmodot.Core.AI.Perception;

using Implementation.AI.Perception.Strategies;

/// <summary>
/// Tracks a single sensor's contribution to a target's perception state.
/// Each contribution has its own lifecycle: active → decaying → dead.
/// Active contributions return raw BaseConfidence; decaying contributions
/// delegate to their decay strategy for time-based confidence calculation.
/// </summary>
internal class SensorContribution
{
    public float BaseConfidence;
    public Vector3 Position;
    public Vector3 Velocity;
    public MemoryDecayStrategy DecayStrategy = null!;
    public bool SensingActive;
    public ulong LastUpdateTime;
    public ulong ExitTime;

    public float CurrentConfidence => SensingActive
        ? BaseConfidence
        : DecayStrategy?.CalculateConfidence(BaseConfidence,
            (Time.GetTicksMsec() - ExitTime) / 1000f) ?? 0f;

    public bool IsAlive => CurrentConfidence > 0.001f;
}
