namespace Jmodot.Implementation.AI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.AI.Emotions;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.AI.Emotions;

/// <summary>
///     Utility AI consideration modifier that adjusts scores based on an emotion's current
///     intensity. Mirrors <see cref="AffinityConsiderationModifier"/> but reads from
///     <see cref="IEmotionalStateProvider"/> instead of affinities.
///     Example: Fear intensity boosts "Flee" consideration score.
/// </summary>
[GlobalClass]
public partial class EmotionConsiderationModifier : ConsiderationModifier
{
    /// <summary>The emotion to use for modification.</summary>
    [Export]
    public EmotionType? TargetEmotion { get; set; }

    /// <summary>How much the emotion affects the score. Higher = stronger effect.</summary>
    [Export(PropertyHint.Range, "0.1, 3.0, 0.05")]
    public float Multiplier { get; set; } = 1.5f;

    /// <summary>
    /// Optional curve for non-linear response. If set, uses curve instead of linear multiplier.
    /// X-axis: emotion intensity (0-1), Y-axis: multiplier value.
    /// </summary>
    [Export]
    public Curve? ResponseCurve { get; set; }

    public override float Modify(float baseScore, IBlackboard blackboard)
    {
        if (TargetEmotion == null) { return baseScore; }

        if (!blackboard.TryGet<AIEmotionalStateComponent>(BBDataSig.EmotionalState, out var emotions) || emotions == null)
        {
            return baseScore;
        }

        if (!emotions.TryGetIntensity(TargetEmotion, out float emotionValue))
        {
            // Emotion not active — treat as 0 intensity
            emotionValue = 0f;
        }

        float effectiveMultiplier = ResponseCurve != null
            ? ResponseCurve.Sample(emotionValue)
            : 1f + (emotionValue * Multiplier);

        return baseScore * effectiveMultiplier;
    }
}
