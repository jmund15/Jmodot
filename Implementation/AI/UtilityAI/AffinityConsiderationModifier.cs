// --- AffinityConsiderationModifier.cs ---
namespace JmoAI.UtilityAI;

using Godot;
using Jmodot.Core.AI.Affinities;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.Affinities;
using Jmodot.Implementation.AI.BB;

/// <summary>
/// Generalized affinity modifier that adjusts consideration scores based on any affinity.
/// Replaces hardcoded FearAffinityModifier with a data-driven approach.
/// </summary>
[GlobalClass]
public partial class AffinityConsiderationModifier : ConsiderationModifier
{
    /// <summary>
    /// The affinity to use for modification. Drag an Affinity resource here.
    /// </summary>
    [Export]
    public Affinity? TargetAffinity { get; set; }

    /// <summary>
    /// How much the affinity affects the score. Higher = stronger effect.
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 3.0, 0.05")]
    public float Multiplier { get; set; } = 1.5f;

    /// <summary>
    /// Optional curve for non-linear response. If set, uses curve instead of linear multiplier.
    /// X-axis: affinity value (0-1), Y-axis: multiplier value.
    /// </summary>
    [Export]
    public Curve? ResponseCurve { get; set; }

    public override float Modify(float baseScore, IBlackboard blackboard)
    {
        if (TargetAffinity == null)
        {
            return baseScore;
        }

        if (!blackboard.TryGet<AIAffinitiesComponent>(BBDataSig.Affinities, out var affinities) || affinities == null)
        {
            return baseScore;
        }

        if (!affinities.TryGetAffinity(TargetAffinity, out float affinityValue))
        {
            return baseScore;
        }

        // Use curve if provided, otherwise linear multiplier
        float effectiveMultiplier = ResponseCurve != null
            ? ResponseCurve.Sample(affinityValue)
            : 1f + (affinityValue * Multiplier);

        return baseScore * effectiveMultiplier;
    }
}
