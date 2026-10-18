namespace Jmodot.Implementation.AI.Navigation.Goals;

using Jmodot.Implementation.AI.Navigation.Targets;

/// <summary>
/// A goal's distance geometry for cross-slot checks: the band it acts in and whether that band
/// describes one member or a whole set. A report exists before any live target is resolved,
/// because it comes from the authored target shape.
/// </summary>
public readonly struct GoalDistanceBand
{
    public readonly float Min;
    public readonly float Max;
    public readonly TargetSampleGeometry Geometry;

    public GoalDistanceBand(float min, float max, TargetSampleGeometry geometry)
    {
        Min = min;
        Max = max;
        Geometry = geometry;
    }
}
