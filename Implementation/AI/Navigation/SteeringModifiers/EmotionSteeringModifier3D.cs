namespace Jmodot.Implementation.AI.Navigation.SteeringModifiers;

using System.Collections.Generic;
using System.Linq;
using BB;
using Core.AI.BB;
using Core.AI.Emotions;
using Core.AI.Navigation.SteeringModifiers;
using Emotions;
using Jmodot.Core.Shared.Attributes;
using Shared;

/// <summary>
///     Steering modifier that scales directional scores based on an emotion's current intensity.
///     Mirrors <see cref="AffinitySteeringModifier3D"/> but reads from <see cref="IEmotionalStateProvider"/>.
///     Example: High fear intensity amplifies avoidance directions.
/// </summary>
[GlobalClass]
public partial class EmotionSteeringModifier3D : SteeringConsiderationModifier3D
{
    [Export, RequiredExport] private EmotionType _emotionToMeasure = null!;
    [Export, RequiredExport] private Curve _responseCurve = null!;

    public override void Modify(ref Dictionary<Vector3, float> scores, SteeringDecisionContext3D context, IBlackboard blackboard)
    {
        if (_emotionToMeasure == null || _responseCurve == null)
        {
            JmoLogger.Error(this,
                "Modifier is misconfigured. Either 'Emotion To Measure' or 'Response Curve' is not set. It will be skipped.",
                blackboard.Get<Node>(BBDataSig.Agent));
            return;
        }

        if (!blackboard.TryGet<AIEmotionalStateComponent>(BBDataSig.EmotionalState, out var emotions) || emotions == null)
        {
            return;
        }

        if (!emotions.TryGetIntensity(_emotionToMeasure, out float emotionValue))
        {
            return; // Emotion not active — skip modification
        }

        var multiplier = _responseCurve.SampleBaked(emotionValue);

        foreach (var key in scores.Keys.ToList())
        {
            scores[key] *= multiplier;
        }
    }
}
