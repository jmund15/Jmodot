namespace Jmodot.Implementation.AI.BehaviorTree.Tasks.DitherPicks;

using Core.AI.BehaviorTree;

/// <summary>
/// Draws a uniformly random member of the set on every flip — an erratic, unpredictable weave. The
/// draw comes entirely from the injected stream, so one authored instance serves every agent
/// dithering at once and the sequence stays reproducible per agent seed.
/// </summary>
[GlobalClass, Tool]
public partial class RandomDitherPick : DitherPickStrategy
{
    /// <inheritdoc />
    public override Vector3 Pick(in DitherPickContext ctx)
    {
        var ordered = ctx.Directions.OrderedDirections;
        return ordered.Count == 0 ? Vector3.Zero : ordered[ctx.Rng.GetRndInt(ordered.Count)];
    }
}
