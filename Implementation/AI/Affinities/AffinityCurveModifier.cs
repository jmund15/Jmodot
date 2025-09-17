#region

using Jmodot.Core.AI.Affinities;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.AI.BehaviorTree.Utility;
using Jmodot.Implementation.Shared;

#endregion

namespace Jmodot.Implementation.AI.Affinities;

/// <summary>
///     A powerful, data-driven modifier that shapes a utility score based on an AI's
///     affinity. It uses a designer-friendly Curve resource to translate an affinity
///     value (e.g., Fear) into a score multiplier, allowing for complex, non-linear
///     behavioral tuning.
/// </summary>
[GlobalClass]
public partial class AffinityCurveModifier : ConsiderationModifier
{
    /// <summary>
    ///     The specific affinity from the AIAffinitiesComponent to read from.
    /// </summary>
    [Export] private Affinity _affinityToMeasure = null!;

    /// <summary>
    ///     The Curve resource that defines the response to the affinity.
    ///     X-axis: Affinity Value (0 to 1)
    ///     Y-axis: Score Multiplier
    /// </summary>
    [Export] private Curve _responseCurve = null!;

    public override float Modify(float baseScore, IBlackboard blackboard)
    {
        // A missing curve is a configuration error, so we fail gracefully and log it.
        if (_responseCurve == null)
        {
            JmoLogger.Warning(
                this,
                "AffinityCurveModifier on {0} is missing a ResponseCurve resource. Returning base score unmodified.",
                blackboard.GetVar<Node>(BBDataSig.Agent),
                blackboard.GetVar<Node>(BBDataSig.Agent)!.Name);
            return baseScore;
        }

        // Get the affinities component from the blackboard.
        var affinities = blackboard.GetVar<AIAffinitiesComponent>(BBDataSig.Affinities);
        if (affinities == null)
        {
            JmoLogger.Warning(
                this,
                "AffinityCurveModifier on {0} could not find an AIAffinitiesComponent in the blackboard. Returning base score unmodified.",
                blackboard.GetVar<Node>(BBDataSig.Agent),
                blackboard.GetVar<Node>(BBDataSig.Agent)!.Name);
            // If this AI has no affinities, there's nothing to measure, so we don't modify.
            return baseScore;
        }

        // 1. Get the current value of the chosen affinity (e.g., Fear = 0.8).
        if (!affinities.TryGetAffinity(_affinityToMeasure, out float affinityValue))
        {
            JmoLogger.Warning(
                this,
                "AffinityCurveModifier on {0} could not find the affinity '{1}' in the AIAffinitiesComponent. Returning base score unmodified.",
                blackboard.GetVar<Node>(BBDataSig.Agent),
                blackboard.GetVar<Node>(BBDataSig.Agent)!.Name,
                _affinityToMeasure.AffinityName);
            return baseScore;
        }

        // 2. Sample the curve at that value to get the multiplier.
        //    The curve's X-axis should be designed to be read from 0 to 1.
        var multiplier = _responseCurve.SampleBaked(affinityValue);

        // 3. Apply the multiplier to the base score.
        return baseScore * multiplier;
    }
}