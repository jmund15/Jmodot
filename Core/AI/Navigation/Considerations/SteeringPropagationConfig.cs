namespace Jmodot.Core.AI.Navigation.Considerations;

/// <summary>
/// Reusable config resource for score propagation (neighbor bleed-over) in steering
/// considerations. When attached to a BaseAIConsideration3D via export, propagation
/// is applied automatically in Evaluate() after CalculateBaseScores.
///
/// Defaults: NeighborCount=2, DiminishWeight=0.5, PropagateNegative=false.
/// These match the historic defaults of all considerations except StaticBody3DConsideration
/// (which uses PropagateNegative=true).
///
/// Assign to a consideration to enable propagation; set to null to disable.
/// </summary>
[GlobalClass, Tool]
public partial class SteeringPropagationConfig : Resource
{
    [Export(PropertyHint.Range, "1, 8, 1")]
    public int NeighborCount { get; set; } = 2;

    [Export(PropertyHint.Range, "0.1, 0.9, 0.05")]
    public float DiminishWeight { get; set; } = 0.5f;

    [Export]
    public bool PropagateNegative { get; set; }
}
