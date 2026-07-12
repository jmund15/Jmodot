namespace Jmodot.Core.AI.Navigation.Considerations;

/// <summary>
/// Reusable config resource for score propagation (neighbor bleed-over) in steering
/// considerations. When attached to a BaseAIConsideration3D via export, propagation
/// is applied automatically in Evaluate() after CalculateBaseScores. Bleed is symmetric in
/// sign — positives spread interest, negatives spread a danger gradient to neighbors.
///
/// Defaults: NeighborCount=2, DiminishWeight=0.5.
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
}
