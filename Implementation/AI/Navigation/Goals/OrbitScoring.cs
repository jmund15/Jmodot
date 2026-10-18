namespace Jmodot.Implementation.AI.Navigation.Goals;

using Godot;

/// <summary>
/// The pure orbit arithmetic shared by the orbit goal and the kept orbit consideration: the
/// tangential-plus-radial desired vector, the mirror sign, the hold rule and the flip clock.
/// Every input is passed in; no state lives here.
/// </summary>
public static class OrbitScoring
{
    /// <summary>Horizontal distance under which an agent is treated as on the target.</summary>
    public const float CoLocatedDistance = 0.05f;

    /// <summary>How much of the tangent is given up at full radial correction.</summary>
    public const float TangentAttenuation = 0.6f;

    /// <summary>Softness floor: below it the radial ramp would spike.</summary>
    public const float SoftnessFloor = 0.1f;

    public const float HoldLeaveMultiple = 2f;

    public const float HoldStandLeaveMultiple = 1.5f;

    /// <summary>
    /// The unit direction that follows the ring: a tangent at <paramref name="sign"/> with a radial
    /// correction that grows as the agent leaves <paramref name="radius"/>. Zero when the agent sits
    /// on the target or the vector cancels.
    /// </summary>
    public static Vector3 Desired(Vector3 toTarget, float radius, float softness, float sign)
    {
        toTarget.Y = 0f;
        var distance = toTarget.Length();
        if (distance < CoLocatedDistance) { return Vector3.Zero; }

        var toTargetDir = toTarget / distance;
        var tangent = Vector3.Up.Cross(toTargetDir) * sign;
        var radialFactor = Mathf.Clamp(
            (distance - radius) / Mathf.Max(SoftnessFloor, softness), -1f, 1f);
        var desired = tangent * (1f - TangentAttenuation * Mathf.Abs(radialFactor)) + toTargetDir * radialFactor;
        return desired.LengthSquared() < 0.0001f ? Vector3.Zero : desired.Normalized();
    }

    /// <summary>
    /// The sign that follows a target's sideways motion, or 0 when it is standing still or drifting
    /// below <paramref name="deadzone"/>.
    /// </summary>
    public static float MirrorSign(Vector3 targetVelocity, Vector3 tangent, float deadzone)
    {
        float lateral = targetVelocity.Dot(tangent);
        return float.IsFinite(lateral) && Mathf.Abs(lateral) > Threshold(deadzone) ? Mathf.Sign(lateral) : 0f;
    }

    /// <summary>
    /// Advances the flip countdown by <paramref name="elapsed"/>, flipping the sign once per whole
    /// interval crossed and keeping the remainder. A non-positive or non-finite interval never
    /// flips; an invalid delta does not advance.
    /// </summary>
    public static (float Sign, float TimeToFlip) Flip(float sign, float timeToFlip, float interval,
        float elapsed)
    {
        if (!(interval > 0f) || !float.IsFinite(interval)) { return (sign, timeToFlip); }
        if (!float.IsFinite(elapsed) || !(elapsed > 0f)) { return (sign, timeToFlip); }

        var remaining = timeToFlip - elapsed;
        while (remaining <= 0f)
        {
            sign = -sign;
            remaining += interval;
        }
        return (sign, remaining);
    }

    /// <summary>
    /// Whether a mirroring agent holds ground this tick: the target is not sidestepping, is standing
    /// (with a wider stand threshold once already holding), and the agent is close to the ring.
    /// </summary>
    public static bool UpdateHold(float sign, Vector3 targetVelocity, float radialError,
        bool wasHolding, float holdBand, float standSpeed)
    {
        float enter = float.IsFinite(holdBand) ? Mathf.Max(0f, holdBand) : 0f;
        float leave = HoldLeaveMultiple * enter;
        float targetSpeed = new Vector2(targetVelocity.X, targetVelocity.Z).Length();
        bool targetStands = float.IsFinite(targetSpeed)
            && targetSpeed <= Threshold(standSpeed) * (wasHolding ? HoldStandLeaveMultiple : 1f);
        return sign == 0f && targetStands
            && Mathf.Abs(radialError) < (wasHolding ? leave : enter);
    }

    private static float Threshold(float authored)
        => float.IsFinite(authored) ? Mathf.Max(0f, authored) : 0.3f;
}
