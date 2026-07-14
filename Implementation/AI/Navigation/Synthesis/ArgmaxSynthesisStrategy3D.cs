namespace Jmodot.Implementation.AI.Navigation.Synthesis;

using Godot;
using Core.AI.Navigation;

/// <summary>
/// Default synthesis: pick the single best-effective UNMASKED bin (no vector-sum cancellation),
/// hold it under jitter via a commit margin, and optionally interpolate within the winning bin using
/// its two ring neighbors. Hard-masked bins are excluded outright; all-masked falls back to the
/// least-danger bin.
/// </summary>
[GlobalClass, Tool]
public sealed partial class ArgmaxSynthesisStrategy3D : SteeringSynthesisStrategy3D
{
    /// <summary>A challenger must beat the committed bin's effective score by this fraction to steal
    /// commitment. 0 disables hysteresis.</summary>
    [Export(PropertyHint.Range, "0.0, 0.5, 0.01")] private float _commitMargin = 0.10f;

    /// <summary>When true (and the direction set is a circular ring), blend the winner with its two
    /// neighbors weighted by their own effective score; otherwise the raw winner bin is returned.</summary>
    [Export] private bool _interpolateWithNeighbors = true;

    public override (Vector3 Direction, SteeringSynthesisState NewState) Synthesize(
        SteeringContextMap map, in SteeringSynthesisState state)
    {
        int n = map.Bins.Count;
        if (n == 0) { return (Vector3.Zero, SteeringSynthesisState.Empty); }

        // 1. Every bin hard-masked -> least-danger bin (no interest to commit to).
        if (map.AllMasked)
        {
            int least = LeastDangerBin(map);
            return (map.Bins[least], new SteeringSynthesisState { CommittedBin = -1, LastDirection = map.Bins[least] });
        }

        // 2. No positive interest anywhere -> idle.
        if (!map.HasAnyInterest)
        {
            return (Vector3.Zero, SteeringSynthesisState.Empty);
        }

        // Argmax over UNMASKED bins by effective score (>= 1 unmasked guaranteed after step 1).
        // Shared with the debug recorder via SteeringContextMap so both rank bins identically.
        int best = map.ArgmaxUnmasked(DangerScale);
        float bestScore = map.EffectiveScore(best, DangerScale);

        // 3. Commitment: hold the committed bin unless a challenger clears committed*(1 + margin).
        int committed = state.CommittedBin;
        bool committedValid = committed >= 0 && committed < n
            && !map.HardMask[committed] && map.EffectiveScore(committed, DangerScale) > 0f;
        int winner;
        if (committedValid)
        {
            float committedScore = map.EffectiveScore(committed, DangerScale);
            winner = (best != committed && bestScore > committedScore * (1f + _commitMargin)) ? best : committed;
        }
        else
        {
            winner = best;
        }

        float winnerScore = map.EffectiveScore(winner, DangerScale);

        // 4. Even the best effective score is non-positive -> stop (danger everywhere).
        if (winnerScore <= 0f)
        {
            return (Vector3.Zero, new SteeringSynthesisState { CommittedBin = -1, LastDirection = Vector3.Zero });
        }

        // 5. Interpolate within the winning bin (self-weighted + two ring neighbors) when enabled AND
        //    the set forms a ring; otherwise the raw winner bin (the graceful-degradation promise).
        Vector3 dir;
        if (_interpolateWithNeighbors && map.HasCircularOrder)
        {
            Vector3 acc = map.Bins[winner] * winnerScore;
            acc += NeighborContribution(map, (winner - 1 + n) % n);
            acc += NeighborContribution(map, (winner + 1) % n);
            dir = acc.Normalized();
        }
        else
        {
            dir = map.Bins[winner];
        }

        return (dir, new SteeringSynthesisState { CommittedBin = winner, LastDirection = dir });
    }

    private Vector3 NeighborContribution(SteeringContextMap map, int bin)
    {
        if (map.HardMask[bin]) { return Vector3.Zero; }
        float s = map.EffectiveScore(bin, DangerScale);
        return s > 0f ? map.Bins[bin] * s : Vector3.Zero;
    }

    #region Test Helpers
#if TOOLS
    internal void SetCommitMargin(float value) => _commitMargin = value;
    internal void SetInterpolateWithNeighbors(bool value) => _interpolateWithNeighbors = value;
#endif
    #endregion
}
