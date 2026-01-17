// --- StatConsiderationModifier.cs ---
namespace JmoAI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Stats;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

/// <summary>
/// A consideration modifier that scales utility scores based on a stat value.
/// Uses a response curve to translate stat values into score multipliers.
/// This allows AI behavior to be influenced by character statistics
/// (e.g., low stamina reduces attack urgency).
/// </summary>
[GlobalClass]
public partial class StatConsiderationModifier : ConsiderationModifier
{
    /// <summary>
    /// The attribute to read from the stat provider.
    /// </summary>
    [Export]
    public Attribute TargetAttribute { get; set; } = null!;

    /// <summary>
    /// The minimum expected stat value (maps to X=0 on the response curve).
    /// </summary>
    [Export]
    public float MinValue { get; set; } = 0f;

    /// <summary>
    /// The maximum expected stat value (maps to X=1 on the response curve).
    /// </summary>
    [Export]
    public float MaxValue { get; set; } = 100f;

    /// <summary>
    /// Curve that translates the normalized stat value into a score multiplier.
    /// X-axis: Normalized stat value (0 to 1)
    /// Y-axis: Multiplier applied to the base score
    /// </summary>
    /// <remarks>
    /// Example curves:
    /// - Linear (0,0)→(1,1): Multiplier matches stat percentage
    /// - Threshold (0,0)→(0.3,0)→(0.3,1)→(1,1): No modification above 30%, full penalty below
    /// - Inverted (0,1)→(1,0): High stat reduces score
    /// </remarks>
    [Export]
    public Curve ResponseCurve { get; set; } = null!;

    public override float Modify(float baseScore, IBlackboard blackboard)
    {
        // 1. Validate response curve
        if (ResponseCurve == null)
        {
            JmoLogger.Warning(
                this,
                "StatConsiderationModifier is missing a ResponseCurve resource. Returning base score unmodified."
            );
            return baseScore;
        }

        // 2. Get stat provider from blackboard
        if (!blackboard.TryGet<IStatProvider>(BBDataSig.Stats, out var stats) || stats == null)
        {
            return baseScore;
        }

        // 3. Validate attribute is configured
        if (TargetAttribute == null)
        {
            JmoLogger.Warning(this, "StatConsiderationModifier: TargetAttribute is not configured.");
            return baseScore;
        }

        // 4. Get the stat value
        float statValue = stats.GetStatValue<float>(TargetAttribute, 0f);

        // 5. Normalize to 0-1 range
        float range = MaxValue - MinValue;
        if (range <= 0f)
        {
            return baseScore;
        }

        float normalized = Mathf.Clamp((statValue - MinValue) / range, 0f, 1f);

        // 6. Sample the curve to get the multiplier
        float multiplier = ResponseCurve.SampleBaked(normalized);

        // 7. Apply multiplier to base score
        return baseScore * multiplier;
    }
}
