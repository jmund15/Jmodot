namespace Jmodot.Implementation.Actors;

/// <summary>
/// Pure static evaluator for source-based control-loss detection. Replaces the
/// raw-velocity-magnitude trigger with a two-axis OR over independent hysteresis
/// bands — one for force-provider aggregate, one for velocity-offset-provider
/// aggregate. Combat impulses (which never register as providers) are invisible
/// here by design, so they cannot trigger control loss.
/// </summary>
/// <remarks>
/// Architectural intent: distinguish "captured by sustained environmental force"
/// (CapturedState pipeline) from "transient combat impulse" (HitState/ReelingState/
/// TumbleState pipeline) at the trigger boundary instead of the state-machine
/// transition layer. Aligns with the IForceProvider3D abstraction's contract:
/// providers represent sustained forces; impulses bypass it entirely.
/// </remarks>
public static class SourceBasedControlLossEvaluator
{
    /// <summary>
    /// Returns whether the entity should be in a control-lost state given the
    /// current per-frame aggregate magnitudes from each provider axis.
    /// </summary>
    /// <param name="forceMagnitude">Magnitude of <c>ExternalForceReceiver3D.GetTotalForce</c>.</param>
    /// <param name="offsetMagnitude">Magnitude of <c>ExternalForceReceiver3D.GetTotalVelocityOffset</c>.</param>
    /// <param name="isCurrentlyLost">Whether control is currently lost (for hysteresis branching).</param>
    /// <param name="forceLossThreshold">Force magnitude above which control is lost.</param>
    /// <param name="forceRegainThreshold">Force magnitude below which the force axis releases.</param>
    /// <param name="offsetLossThreshold">Offset magnitude above which control is lost.</param>
    /// <param name="offsetRegainThreshold">Offset magnitude below which the offset axis releases.</param>
    /// <returns>True if either axis indicates control should be lost; false only when both axes agree control is normal.</returns>
    public static bool ShouldLoseControl(
        float forceMagnitude,
        float offsetMagnitude,
        bool isCurrentlyLost,
        float forceLossThreshold,
        float forceRegainThreshold,
        float offsetLossThreshold,
        float offsetRegainThreshold)
    {
        var forceLost = ControlLossEvaluator.Evaluate(
            forceMagnitude, isCurrentlyLost, forceLossThreshold, forceRegainThreshold);

        var offsetLost = ControlLossEvaluator.Evaluate(
            offsetMagnitude, isCurrentlyLost, offsetLossThreshold, offsetRegainThreshold);

        return forceLost || offsetLost;
    }
}
