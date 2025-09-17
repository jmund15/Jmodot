namespace Jmodot.Core.AI.Perception.Strategies;

/// <summary>
///     Defines a contract for a "strategy" that calculates how an AI's memory confidence decays over time.
///     This enables a data-driven approach, allowing designers to create various decay models (linear, exponential, etc.)
///     and apply them to different sensors or objects to fine-tune AI behavior.
/// </summary>
public interface IMemoryDecayStrategy
{
    /// <summary>
    ///     Calculates the current, time-decayed confidence of a memory.
    /// </summary>
    /// <param name="baseConfidence">The confidence value when the memory was last updated.</param>
    /// <param name="timeSinceUpdate">The time in seconds since the last update.</param>
    /// <returns>The new confidence value, typically clamped between 0.0 and 1.0.</returns>
    float CalculateConfidence(float baseConfidence, float timeSinceUpdate);
}
