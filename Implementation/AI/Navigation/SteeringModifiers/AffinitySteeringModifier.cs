#region

using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.AI.Affinities;
using Jmodot.Core.AI.BB;
using Jmodot.Core.AI.Navigation.SteeringModifiers;
using Jmodot.Implementation.AI.Affinities;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

#endregion

namespace Jmodot.Implementation.AI.Navigation.SteeringModifiers;

/// <summary>
///     A powerful steering modifier that scales directional scores based on an AI's affinity.
///     It uses a Curve resource to translate an affinity value into a multiplier, allowing designers
///     to visually sculpt how personality traits affect movement choices.
/// </summary>
[GlobalClass]
public partial class AffinitySteeringModifier : SteeringConsiderationModifier
{
    [Export] private Affinity _affinityToMeasure = null!;
    [Export] private Curve _responseCurve = null!;

    public override void Modify(ref Dictionary<Vector3, float> scores, DecisionContext context, IBlackboard blackboard)
    {
        // --- Configuration Validation ---
        if (_affinityToMeasure == null || _responseCurve == null)
        {
            JmoLogger.Error(this,
                "Modifier is misconfigured. Either 'Affinity To Measure' or 'Response Curve' is not set. It will be skipped.",
                blackboard.GetVar<Node>(BBDataSig.Agent));
            return;
        }

        var affinities = blackboard.GetVar<AIAffinitiesComponent>(BBDataSig.Affinities);
        if (affinities == null) return; // Agent will have logged this critical error already.

        // --- Core Logic ---
        if (!affinities.TryGetAffinity(_affinityToMeasure, out float affinityValue))
        {
            // This is a recoverable state; the AI just doesn't have this personality trait.
            JmoLogger.Warning(
                this,
                "AffinitySteeringModifier could not find the affinity '{0}' in the AIAffinitiesComponent. It will be skipped.",
                blackboard.GetVar<Node>(BBDataSig.Agent),
                _affinityToMeasure.AffinityName);
            return;
        }

        var multiplier = _responseCurve.SampleBaked(affinityValue);

        foreach (var key in scores.Keys.ToList()) scores[key] *= multiplier;
    }
}