namespace Jmodot.Implementation.Actors;

/// <summary>
/// Pure static evaluator for control-loss detection with hysteresis.
/// Determines whether an entity has lost control based on velocity magnitude
/// and separate loss/regain thresholds to prevent flickering.
/// </summary>
public static class ControlLossEvaluator
{
    /// <summary>
    /// Evaluates whether the entity should be in a control-lost state.
    /// Uses hysteresis: higher threshold to enter, lower threshold to exit.
    /// </summary>
    /// <param name="velocityMagnitude">Current velocity magnitude of the entity.</param>
    /// <param name="isCurrentlyLost">Whether control is currently lost.</param>
    /// <param name="lossThreshold">Velocity magnitude above which control is lost.</param>
    /// <param name="regainThreshold">Velocity magnitude below which control is regained.</param>
    /// <returns>True if control should be lost, false if control should be normal.</returns>
    public static bool Evaluate(
        float velocityMagnitude,
        bool isCurrentlyLost,
        float lossThreshold,
        float regainThreshold)
    {
        if (!isCurrentlyLost)
        {
            return velocityMagnitude >= lossThreshold;
        }

        // Currently lost — only regain when below regain threshold
        return velocityMagnitude > regainThreshold;
    }
}
