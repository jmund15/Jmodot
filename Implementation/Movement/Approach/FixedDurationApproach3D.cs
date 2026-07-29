namespace Jmodot.Implementation.Movement.Approach;

using Godot;

/// <summary>
/// Constant-time approach: the trip takes <see cref="Duration"/> seconds no matter how the target moves.
/// Progress derives from elapsed time alone, so chasing a retreating target neither extends nor shortens
/// the trip — it only steepens the remaining path.
/// </summary>
[GlobalClass, Tool]
public partial class FixedDurationApproach3D : ApproachProfile3D
{
    /// <summary>Seconds the approach takes end to end. Values ≤ 0 arrive instantly.</summary>
    [Export] public float Duration { get; private set; } = 0.35f;

    /// <summary>Optional 0→1 easing over normalized progress. Unset means linear.</summary>
    [Export] public Curve? Easing { get; private set; }

    public override Vector3 Step(Vector3 current, Vector3 target, float elapsed, float delta)
    {
        if (Duration <= 0f) { return target; }
        if (elapsed + delta >= Duration) { return target; }

        float progressNow = SampleProgress(elapsed / Duration);
        if (progressNow >= 1f) { return target; }

        float progressNext = SampleProgress((elapsed + delta) / Duration);
        // Progress is measured from the captured start, but the profile only sees the CURRENT position,
        // so the per-step fraction is renormalized over the gap that is still left to cover.
        float stepFraction = (progressNext - progressNow) / (1f - progressNow);
        return current.Lerp(target, Mathf.Clamp(stepFraction, 0f, 1f));
    }

    public override bool IsComplete(Vector3 current, Vector3 target, float elapsed)
    {
        return Duration <= 0f || elapsed >= Duration;
    }

    private float SampleProgress(float normalizedTime)
    {
        float t = Mathf.Clamp(normalizedTime, 0f, 1f);
        if (Easing == null) { return t; }

        return Mathf.Clamp(Easing.Sample(t), 0f, 1f);
    }

    #region Test Helpers
#if TOOLS
    internal void SetDurationForTest(float duration) => Duration = duration;
    internal void SetEasingForTest(Curve? easing) => Easing = easing;
#endif
    #endregion
}
