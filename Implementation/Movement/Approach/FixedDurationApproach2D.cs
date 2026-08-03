namespace Jmodot.Implementation.Movement.Approach;

using Godot;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Shared;

/// <summary>
/// Constant-time approach: the trip takes <see cref="DurationDefinition"/> seconds no matter how the
/// target moves.
/// Progress derives from elapsed time alone, so chasing a retreating target neither extends nor shortens
/// the trip — it only steepens the remaining path.
/// </summary>
[GlobalClass, Tool]
public partial class FixedDurationApproach2D : ApproachProfile2D
{
    /// <summary>Seconds the approach takes end to end. Values ≤ 0 arrive instantly.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition DurationDefinition { get; private set; } = null!;

    /// <summary>Optional 0→1 easing over normalized progress. Unset means linear.</summary>
    [Export] public Curve? Easing { get; private set; }

    public override Vector2 Step(Vector2 start, Vector2 current, Vector2 target, float elapsed, float delta, IStatProvider? stats)
    {
        var duration = ResolveDuration(stats);
        if (duration <= 0f) { return target; }
        if (elapsed + delta >= duration) { return target; }

        return start.Lerp(target, SampleProgress((elapsed + delta) / duration));
    }

    public override bool IsComplete(Vector2 current, Vector2 target, float elapsed, IStatProvider? stats)
    {
        var duration = ResolveDuration(stats);
        return duration <= 0f || elapsed >= duration;
    }

    /// <summary>
    /// Resolved per call, never cached: the profile is one shared Resource, so a cached value would be
    /// whichever consumer touched it last. The null check exists so an unauthored duration throws by
    /// NAME at the first step instead of silently gliding at some invisible code default.
    /// </summary>
    private float ResolveDuration(IStatProvider? stats)
    {
        if (DurationDefinition == null) { this.ValidateRequiredExports(); }

        return DurationDefinition.ResolveFloatValue(stats);
    }

    private float SampleProgress(float normalizedTime)
    {
        float t = Mathf.Clamp(normalizedTime, 0f, 1f);
        if (Easing == null) { return t; }

        return Mathf.Clamp(Easing.Sample(t), 0f, 1f);
    }

    #region Test Helpers
#if TOOLS
    internal void SetDurationForTest(float duration) => DurationDefinition = new ConstantFloatDefinition(duration);
    internal void SetEasingForTest(Curve? easing) => Easing = easing;
#endif
    #endregion
}
