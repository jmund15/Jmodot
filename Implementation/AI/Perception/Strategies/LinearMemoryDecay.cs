namespace Jmodot.Implementation.AI.Perception.Strategies;

/// <summary>
///     A concrete, Resource-based implementation of IMemoryDecayStrategy that causes confidence
///     to decrease linearly over a set duration. This provides a common, predictable decay model
///     that can be easily created and configured by designers in the Godot editor.
/// </summary>
[GlobalClass]
public partial class LinearMemoryDecay : MemoryDecayStrategy
{
    /// <summary>
    ///     The total time in seconds it takes for a memory with 1.0 confidence to fully decay to 0.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,60.0,0.1")]
    public float ForgetTime { get; private set; } = 10.0f;

    /// <inheritdoc />
    public override float CalculateConfidence(float baseConfidence, float timeSinceUpdate)
    {
        if (!this.IsEnabled || this.ForgetTime <= 0.0f)
            // A forget time of zero or less means the memory does not decay.
            return baseConfidence;

        // Calculate the total amount of decay that should have occurred.
        var decayAmount = 1.0f / this.ForgetTime * timeSinceUpdate;
        var newConfidence = baseConfidence - decayAmount;

        // Ensure confidence does not fall below zero.
        return Mathf.Max(0.0f, newConfidence);
    }
}
