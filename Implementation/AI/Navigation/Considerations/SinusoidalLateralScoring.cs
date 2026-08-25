namespace Jmodot.Implementation.AI.Navigation.Considerations;

/// <summary>
/// Pure scoring for a lateral sine gait: a direction's alignment with the agent's lateral axis,
/// signed by the current phase of the wave. Callers own the phase; this type owns nothing.
/// </summary>
public static class SinusoidalLateralScoring
{
    /// <summary>
    /// Scores <paramref name="dir"/> against the agent's <paramref name="lateral"/> axis at the
    /// supplied wave <paramref name="phase"/>. Returns a signed value in [-1, 1] — positive is
    /// interest, negative is danger, per the consideration output contract. Returns 0 when the
    /// lateral axis is degenerate or the direction has no horizontal component.
    /// </summary>
    public static float Score(Vector3 dir, Vector3 lateral, float phase)
    {
        if (lateral.LengthSquared() < 0.000001f)
        {
            return 0f;
        }

        var flatDir = new Vector3(dir.X, 0f, dir.Z);
        if (flatDir.LengthSquared() < 0.000001f)
        {
            return 0f;
        }

        return Mathf.Clamp(dir.Normalized().Dot(lateral.Normalized()) * phase, -1f, 1f);
    }
}
