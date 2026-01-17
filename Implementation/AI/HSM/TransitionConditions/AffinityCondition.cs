namespace Jmodot.Implementation.AI.HSM.TransitionConditions;

using Affinities;
using BB;
using Core.AI.Affinities;
using Core.AI.BB;
using Core.AI.HSM;
using Core.Shared.Attributes;
using Examples.AI.HSM.TransitionConditions;
using Shared;

/// <summary>
///     HSM transition condition that evaluates an agent's affinity value against a threshold.
///     Example: Transition to "Flee" state when Fear > 0.7
/// </summary>
[GlobalClass]
public partial class AffinityCondition : TransitionCondition
{
    /// <summary>
    /// The affinity to check. Drag an Affinity resource here.
    /// </summary>
    [Export, RequiredExport]
    public Affinity TargetAffinity { get; set; } = null!;

    /// <summary>
    /// The threshold value to compare against (0-1 range).
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    public float Threshold { get; set; } = 0.5f;

    /// <summary>
    /// How to compare the affinity value to the threshold.
    /// </summary>
    [Export]
    public NumericalConditionType Comparison { get; set; } = NumericalConditionType.GreaterThan;

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (this.TargetAffinity == null)
        {
            JmoLogger.Warning(this, "AffinityCondition: TargetAffinity is not set.", agent);
            return false;
        }

        if (!bb.TryGet<AIAffinitiesComponent>(BBDataSig.Affinities, out var affinities) || affinities == null)
        {
            JmoLogger.Warning(this, "AffinityCondition: No AIAffinitiesComponent found in blackboard.", agent);
            return false;
        }

        float value = affinities.GetAffinity(this.TargetAffinity) ?? 0f;
        return this.Comparison.CalculateFloatCondition(value, this.Threshold);
    }
}
