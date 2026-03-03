namespace Jmodot.Implementation.AI.HSM.TransitionConditions;

using BB;
using Core.AI.BB;
using Core.AI.Emotions;
using Core.AI.HSM;
using Core.Shared.Attributes;
using Emotions;
using Examples.AI.HSM.TransitionConditions;
using Shared;

/// <summary>
///     HSM transition condition that evaluates an agent's current emotional intensity
///     against a threshold. Example: Transition to "Flee" state when Fear > 0.7.
///     Mirrors <see cref="AffinityCondition"/> but reads from <see cref="IEmotionalStateProvider"/>
///     instead of <see cref="Affinities.AIAffinitiesComponent"/>.
/// </summary>
[GlobalClass]
public partial class EmotionCondition : TransitionCondition
{
    /// <summary>The emotion type to check intensity for.</summary>
    [Export, RequiredExport]
    public EmotionType TargetEmotion { get; set; } = null!;

    /// <summary>The threshold value to compare against (0-1 range).</summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    public float Threshold { get; set; } = 0.5f;

    /// <summary>How to compare the emotion intensity to the threshold.</summary>
    [Export]
    public NumericalConditionType Comparison { get; set; } = NumericalConditionType.GreaterThan;

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (TargetEmotion == null)
        {
            JmoLogger.Warning(this, "EmotionCondition: TargetEmotion is not set.", agent);
            return false;
        }

        if (!bb.TryGet<AIEmotionalStateComponent>(BBDataSig.EmotionalState, out var emotions) || emotions == null)
        {
            JmoLogger.Warning(this, "EmotionCondition: No AIEmotionalStateComponent found in blackboard.", agent);
            return false;
        }

        float value = emotions.GetIntensity(TargetEmotion) ?? 0f;
        return Comparison.CalculateFloatCondition(value, Threshold);
    }
}
