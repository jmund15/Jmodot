namespace Jmodot.Implementation.AI.Perception.Strategies;

/// <summary>
/// A decay strategy that provides zero memory after sensing stops.
/// When a sensor loses contact, confidence immediately drops to zero.
/// </summary>
[GlobalClass]
public partial class InstantForgetDecay : MemoryDecayStrategy
{
    /// <inheritdoc />
    public override float CalculateConfidence(float baseConfidence, float timeSinceUpdate)
    {
        if (!IsEnabled) { return baseConfidence; }
        return 0f;
    }
}
