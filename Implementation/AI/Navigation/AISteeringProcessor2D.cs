namespace Jmodot.AI.Navigation;

/// <summary>
/// 2D counterpart to <see cref="AISteeringProcessor3D"/>. This session only ports
/// the turn-rate clamping helper needed by <c>TurnRateProfile2D</c> implementations;
/// the steering/consideration pipeline has not yet been ported.
/// </summary>
public static class AISteeringProcessor2D
{
    /// <summary>
    /// Limits the rotation from previous to desired direction by a maximum angular speed.
    /// Returns the desired direction directly when: rate is 0 (unlimited), previous is zero
    /// (first frame), desired is zero (idle), or the angle is within the allowed rotation.
    /// Exposed as static for testability. Mirrors the 3D version minus the XZ-plane flattening
    /// (2D top-down doesn't need it — Vector2 is already in the gameplay plane).
    /// </summary>
    public static Vector2 ApplyTurnRateLimit(
        Vector2 previous, Vector2 desired, float maxTurnRateDegrees, float delta)
    {
        // No smoothing
        if (maxTurnRateDegrees <= 0f || delta <= 0f)
        {
            return desired;
        }

        // Idle → return zero
        if (desired.IsZeroApprox())
        {
            return Vector2.Zero;
        }

        // First frame / coming from idle → snap to desired
        if (previous.IsZeroApprox())
        {
            return desired;
        }

        if (previous.LengthSquared() < 0.001f || desired.LengthSquared() < 0.001f)
        {
            return desired;
        }

        var prevNorm = previous.Normalized();
        var desiredNorm = desired.Normalized();

        float angleRad = prevNorm.AngleTo(desiredNorm);

        // Already aligned (the AngleTo signed result has magnitude ~0)
        if (Mathf.Abs(angleRad) < 0.001f)
        {
            return desiredNorm;
        }

        float maxRadians = Mathf.DegToRad(maxTurnRateDegrees) * delta;

        // Can reach desired this frame
        if (Mathf.Abs(angleRad) <= maxRadians)
        {
            return desiredNorm;
        }

        // Rotate prevNorm toward desired by clamped angle (signed — preserves turning direction)
        float clamped = Mathf.Sign(angleRad) * maxRadians;
        return prevNorm.Rotated(clamped);
    }
}
