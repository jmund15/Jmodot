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
        if (SampleCurve == null || BaseConsideration == null)
        {
            return 0f;
        }
        return SampleCurve.Sample(BaseConsideration.Evaluate(context));
    }
}
