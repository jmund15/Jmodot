// --- CurveConsideration.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;

/// <summary>
/// Remaps a base consideration's score through a curve for non-linear response.
/// Example: exponential urgency as health decreases.
/// </summary>
[GlobalClass, Tool]
public partial class CurveConsideration : UtilityConsideration
{
    [Export]
    protected Curve? SampleCurve;

    [Export]
    protected UtilityConsideration? BaseConsideration;

    protected override float CalculateBaseScore(IBlackboard context)
    {
        if (BaseConsideration == null)
        {
            return 0f;
        }
        float baseScore = BaseConsideration.Evaluate(context);
        if (SampleCurve == null)
        {
            return baseScore;
        }
        return SampleCurve.Sample(baseScore);
    }
}
