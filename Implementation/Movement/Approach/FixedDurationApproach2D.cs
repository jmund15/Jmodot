namespace Jmodot.Implementation.Movement.Approach;

using Godot;

/// <summary>
/// Constant-time approach: the trip takes <see cref="Duration"/> seconds no matter how the target moves.
/// Progress derives from elapsed time alone, so chasing a retreating target neither extends nor shortens
/// the trip — it only steepens the remaining path.
/// </summary>
[GlobalClass, Tool]
public partial class FixedDurationApproach2D : ApproachProfile2D
{
    /// <summary>Seconds the approach takes end to end. Values ≤ 0 arrive instantly.</summary>
    [Export] public float Duration { get; private set; } = 0.35f;

    /// <summary>Optional 0→1 easing over normalized progress. Unset means linear.</summary>
    [Export] public Curve? Easing { get; private set; }

    public override Vector2 Step(Vector2 start, Vector2 current, Vector2 target, float elapsed, float delta)
    {
        if (Duration <= 0f) { return target; }
        if (elapsed + delta >= Duration) { return target; }

        return start.Lerp(target, SampleProgress((elapsed + delta) / Duration));
    }

    public override bool IsComplete(Vector2 current, Vector2 target, float elapsed)
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
