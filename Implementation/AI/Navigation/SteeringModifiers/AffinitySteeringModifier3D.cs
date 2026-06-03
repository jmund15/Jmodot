namespace Jmodot.Implementation.AI.Navigation.SteeringModifiers;

using System.Collections.Generic;
using System.Linq;
using Affinities;
using BB;
using Core.AI.Affinities;
using Core.AI.BB;
using Core.AI.Navigation.SteeringModifiers;
using Jmodot.Core.Shared.Attributes;
using Shared;

/// <summary>
///     A powerful steering modifier that scales directional scores based on an AI's affinity.
///     It uses a Curve resource to translate an affinity value into a multiplier, allowing designers
///     to visually sculpt how personality traits affect movement choices.
/// </summary>
[GlobalClass]
public partial class AffinitySteeringModifier3D : SteeringConsiderationModifier3D
{
    [Export, RequiredExport] private Affinity _affinityToMeasure = null!;
    [Export, RequiredExport] private Curve _responseCurve = null!;

    public override void Modify(ref Dictionary<Vector3, float> scores, SteeringDecisionContext3D context, IBlackboard blackboard)
    {
        // --- Configuration Validation ---
        if (this._affinityToMeasure == null || this._responseCurve == null)
        {
            JmoLogger.Error(this,
                "Modifier is misconfigured. Either 'Affinity To Measure' or 'Response Curve' is not set. It will be skipped.",
                blackboard.Get<Node>(BBDataSig.Agent));
            return;
        }

        // TryGet (not Get): an agent without an AIAffinitiesComponent has the key absent, which
        // Get would THROW on — defeating this graceful skip. TryGet returns false.
        if (!blackboard.TryGet<AIAffinitiesComponent>(BBDataSig.Affinities, out var affinities) || affinities == null)
        {
            return;
        }

        // --- Core Logic ---
        if (!affinities.TryGetAffinity(this._affinityToMeasure, out float affinityValue))
        {
            // This is a recoverable state; the AI just doesn't have this personality trait.
            JmoLogger.Warning(
                this,
                "AffinitySteeringModifier could not find the affinity '{0}' in the AIAffinitiesComponent. It will be skipped.",
                blackboard.Get<Node>(BBDataSig.Agent), this._affinityToMeasure.AffinityName);
            return;
        }

        var multiplier = this._responseCurve.SampleBaked(affinityValue);

        foreach (var key in scores.Keys.ToList())
        {
            scores[key] *= multiplier;
        }
    }

    #region Test Helpers
#if TOOLS
    /// <summary>Test seam: sets the private exports so the affinity-lookup path is reachable.</summary>
    internal void _TestSetExports(Affinity affinity, Curve curve)
    {
        this._affinityToMeasure = affinity;
        this._responseCurve = curve;
    }
#endif
    #endregion
}
