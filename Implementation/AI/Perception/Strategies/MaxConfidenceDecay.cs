namespace Jmodot.Implementation.AI.Perception.Strategies;

using System.Collections.Generic;
using System.Linq;

/// <summary>
///     Composes several decay strategies into one: confidence at any instant is the highest any
///     contributor reports, so a memory persists as long as its most persistent source. The fold is
///     defined over behaviour rather than configuration, so it holds for every strategy subclass —
///     including ones with no comparable "forget time" knob.
/// </summary>
/// <remarks>
///     Commutative and associative, which is the whole point: equal-priority candidates compose to
///     the same result no matter what order the authoring arrays happened to be in. Built at runtime
///     by <see cref="Core.Identification.Identity.ResolvePerceptionDecay" />; deliberately not
///     <c>[GlobalClass]</c> because it is never authored in the Inspector nor serialised to disk.
/// </remarks>
public sealed partial class MaxConfidenceDecay : MemoryDecayStrategy
{
    private MemoryDecayStrategy[] _contributors = System.Array.Empty<MemoryDecayStrategy>();

    /// <summary>
    ///     Folds <paramref name="contributors" /> into a single strategy. Callers should skip this
    ///     for a single contributor and use it directly — the fold is only meaningful for 2+.
    /// </summary>
    public static MaxConfidenceDecay Over(IEnumerable<MemoryDecayStrategy> contributors)
    {
        return new MaxConfidenceDecay { _contributors = contributors.ToArray() };
    }

    /// <inheritdoc />
    public override float CalculateConfidence(float baseConfidence, float timeSinceUpdate)
    {
        if (!this.IsEnabled) { return baseConfidence; }

        var best = 0f;
        foreach (var contributor in this._contributors)
        {
            // A disabled contributor returns baseConfidence undecayed, which is the slowest possible
            // forgetting — so it dominates the fold. That is the consistent reading of IsEnabled.
            var confidence = contributor.CalculateConfidence(baseConfidence, timeSinceUpdate);
            if (confidence > best) { best = confidence; }
        }

        return best;
    }
}
