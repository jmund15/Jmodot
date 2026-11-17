namespace Jmodot.Implementation.Combat;

using Godot;

/// <summary>Stateless resolved-push to pause-duration mapping.</summary>
public static class KnockbackHold
{
    /// <summary>Returns real seconds, or zero for invalid configuration or non-finite resolved inputs.</summary>
    public static float Seconds(float impulse, float speedChange, KnockbackHoldConfig cfg)
    {
        if (!float.IsFinite(impulse) || !float.IsFinite(speedChange) || !cfg.IsValid) { return 0f; }
        float strength = cfg.ImpulseWeight * Mathf.Clamp(impulse / cfg.ImpulseReference, 0f, 1f)
            + (1f - cfg.ImpulseWeight) * Mathf.Clamp(speedChange / cfg.SpeedChangeReference, 0f, 1f);
        if (strength < cfg.DeadZone) { return 0f; }
        float sample = cfg.Shape!.Sample((strength - cfg.DeadZone) / (1f - cfg.DeadZone));
        return ClampSampleSeconds(sample, cfg.MaxSeconds);
    }

    private static float ClampSampleSeconds(float sample, float maxSeconds)
    {
        if (!float.IsFinite(sample) || !float.IsFinite(maxSeconds) || !(maxSeconds > 0f)) { return 0f; }
        float seconds = sample * maxSeconds;
        return float.IsFinite(seconds) ? Mathf.Clamp(seconds, 0f, maxSeconds) : 0f;
    }

    #region Test Helpers
#if TOOLS
    internal static float SecondsFromSampleForTesting(float sample, float maxSeconds) => ClampSampleSeconds(sample, maxSeconds);
#endif
    #endregion
}
