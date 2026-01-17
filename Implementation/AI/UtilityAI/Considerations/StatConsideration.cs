// --- StatConsideration.cs ---
namespace JmoAI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Stats;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

/// <summary>
/// A utility consideration that scores based on a stat value from the IStatProvider.
/// The stat value is normalized between MinValue and MaxValue, then optionally
/// transformed through a response curve. This enables AI decisions to be driven
/// by character statistics (e.g., low stamina reduces attack score).
/// </summary>
[GlobalClass, Tool]
public partial class StatConsideration : UtilityConsideration
{
    /// <summary>
    /// The attribute to read from the stat provider.
    /// </summary>
    [Export]
    public Attribute TargetAttribute { get; set; } = null!;

    /// <summary>
    /// The minimum expected value of the stat (maps to 0 in the normalized score).
    /// </summary>
    [Export]
    public float MinValue { get; set; } = 0f;

    /// <summary>
    /// The maximum expected value of the stat (maps to 1 in the normalized score).
    /// </summary>
    [Export]
    public float MaxValue { get; set; } = 100f;

    /// <summary>
    /// If true, the score is inverted (low stat = high score).
    /// Useful for considerations where low values should trigger action (e.g., low health = high flee urgency).
    /// </summary>
    [Export]
    public bool InvertScore { get; set; } = false;

    /// <summary>
    /// Optional curve to transform the normalized score for non-linear responses.
    /// X-axis: Normalized stat value (0 to 1)
    /// Y-axis: Output score multiplier
    /// </summary>
    [Export]
    public Curve? ResponseCurve { get; set; }

    protected override float CalculateBaseScore(IBlackboard blackboard)
    {
        // 1. Get the stat provider from the blackboard
        if (!blackboard.TryGet<IStatProvider>(BBDataSig.Stats, out var stats) || stats == null)
        {
            return 0f;
        }

        // 2. Validate attribute is configured
        if (TargetAttribute == null)
        {
            JmoLogger.Warning(this, "StatConsideration: TargetAttribute is not configured.");
            return 0f;
        }

        // 3. Get the stat value
        float value = stats.GetStatValue<float>(TargetAttribute, 0f);

        // 4. Normalize the value between MinValue and MaxValue
        float range = MaxValue - MinValue;
        if (range <= 0f)
        {
            JmoLogger.Warning(this, "StatConsideration: MaxValue must be greater than MinValue.");
            return 0f;
        }

        float normalized = (value - MinValue) / range;

        // 5. Clamp to 0-1 range
        normalized = Mathf.Clamp(normalized, 0f, 1f);

        // 6. Apply response curve if configured
        if (ResponseCurve != null)
        {
            normalized = ResponseCurve.SampleBaked(normalized);
        }

        // 7. Invert if requested
        if (InvertScore)
        {
            normalized = 1f - normalized;
        }

        return normalized;
    }
}
