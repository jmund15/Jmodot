namespace Jmodot.Implementation.AI.Perception.Strategies;

/// <summary>
///     Defines a contract for a "strategy" that calculates how an AI's memory confidence decays over time.
///     This enables a data-driven approach, allowing designers to create various decay models (linear, exponential, etc.)
///     and apply them to different sensors or objects to fine-tune AI behavior.
/// </summary>
[GlobalClass]
public abstract partial class MemoryDecayStrategy : Resource
{
    /// <summary>
    ///     A shared property allowing any decay strategy to be easily toggled on or off from the editor.
    ///     If disabled, the CalculateConfidence method will be bypassed, causing the memory not to decay.
    /// </summary>
    [Export]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Calculates the current, time-decayed confidence of a memory.
    /// </summary>
    /// <param name="baseConfidence">The confidence value when the memory was last updated.</param>
    /// <param name="timeSinceUpdate">The time in seconds since the last update.</param>
    /// <returns>The new confidence value, typically clamped between 0.0 and 1.0.</returns>
    public abstract float CalculateConfidence(float baseConfidence, float timeSinceUpdate);
}
