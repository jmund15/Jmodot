namespace Jmodot.Implementation.AI.Navigation;

using System;
using System.Collections;
using System.Collections.Generic;
using Core.AI.Navigation.Considerations;

/// <summary>
/// Captures per-consideration attribution to a <see cref="SteeringContextMap"/> by snapshot/diff:
/// the processor calls <see cref="CaptureBefore"/> / <see cref="CaptureAfter"/> around each
/// consideration's Evaluate, so the channel delta between snapshots IS that consideration's
/// contribution (the map's Interest/Danger channels are strictly additive). Evaluate's signature is
/// never touched — attribution is a processor-loop concern.
///
/// Buffers are lazily allocated on first <see cref="BeginFrame"/> and re-used across frames (no
/// steady-state GC); they resize only when the bin count changes (pooled processor reused with a
/// different DirectionSet). Contribution instances are pooled likewise — a reference returned by
/// <see cref="Contributions"/> is valid only until the next <see cref="BeginFrame"/>.
/// </summary>
public sealed class DebugSteeringRecorder
{
    private float[]? _interestBefore;
    private float[]? _dangerBefore;
    private bool[]? _maskBefore;

    private readonly List<ConsiderationContribution> _pool = new();
    private int _count;

    private SteeringContextMap? _lastMap;
    private float _dangerScale = 1.0f;
    private readonly ContributionView _view;

    /// <summary>The raw per-frame winner: argmax of EffectiveScore (Interest − DangerScale·Danger) over
    /// non-masked bins, -1 if all masked. Computed via the same SteeringContextMap.ArgmaxUnmasked as live
    /// synthesis, so it never diverges under a tuned DangerScale.</summary>
    public int ChosenBin { get; private set; } = -1;

    /// <summary>The synthesis-committed bin (may differ from <see cref="ChosenBin"/> under hysteresis).</summary>
    public int CommittedBin { get; private set; } = -1;

    /// <summary>The live per-consideration contributions captured this frame.</summary>
    public IReadOnlyList<ConsiderationContribution> Contributions => _view;

    public DebugSteeringRecorder()
    {
        _view = new ContributionView(this);
    }

    /// <summary>Resets the per-frame contribution list and sizes buffers to the map's bin count.</summary>
    public void BeginFrame(SteeringContextMap map)
    {
        EnsureSized(map.Bins.Count);
        _count = 0;
        _lastMap = map;
        ChosenBin = -1;
        CommittedBin = -1;
    }

    /// <summary>Snapshots the map's channels before a consideration's Evaluate.</summary>
    public void CaptureBefore(SteeringContextMap map)
    {
        int n = map.Bins.Count;
        EnsureSized(n);
        Array.Copy(map.Interest, _interestBefore!, n);
        Array.Copy(map.Danger, _dangerBefore!, n);
        Array.Copy(map.HardMask, _maskBefore!, n);
    }

    /// <summary>Diffs the map against the pre-Evaluate snapshot and appends the consideration's contribution.</summary>
    public void CaptureAfter(BaseAIConsideration3D consideration, SteeringContextMap map)
    {
        int n = map.Bins.Count;
        var contrib = RentContribution(n);
        contrib.Source = SourceName(consideration);
        for (int i = 0; i < n; i++)
        {
            contrib.InterestDelta[i] = map.Interest[i] - _interestBefore![i];
            contrib.DangerDelta[i] = map.Danger[i] - _dangerBefore![i];
            contrib.MaskAdded[i] = map.HardMask[i] && !_maskBefore![i];
        }
    }

    /// <summary>Records the frame's decision: the raw chosen bin (argmax by the strategy's DangerScale
    /// weighting, so it matches live synthesis) + the synthesis-committed bin.</summary>
    public void RecordDecision(SteeringContextMap map, int committedBin, float dangerScale)
    {
        _lastMap = map;
        _dangerScale = dangerScale;
        CommittedBin = committedBin;
        ChosenBin = map.ArgmaxUnmasked(dangerScale);
    }

    /// <summary>Non-masked bin indices ranked by EffectiveScore (the last RecordDecision's DangerScale),
    /// highest first, capped at <paramref name="n"/>.</summary>
    public IReadOnlyList<int> GetTopBins(int n)
    {
        var map = _lastMap;
        return map == null ? Array.Empty<int>() : map.RankUnmasked(_dangerScale, n);
    }

    private void EnsureSized(int n)
    {
        if (_interestBefore != null && _interestBefore.Length == n) { return; }
        _interestBefore = new float[n];
        _dangerBefore = new float[n];
        _maskBefore = new bool[n];
        // Pooled contributions are bin-sized too — drop the stale-sized pool so RentContribution rebuilds.
        _pool.Clear();
        _count = 0;
    }

    private ConsiderationContribution RentContribution(int n)
    {
        ConsiderationContribution c;
        if (_count < _pool.Count)
        {
            c = _pool[_count];
        }
        else
        {
            c = new ConsiderationContribution
            {
                InterestDelta = new float[n],
                DangerDelta = new float[n],
                MaskAdded = new bool[n],
            };
            _pool.Add(c);
        }
        _count++;
        return c;
    }

    private static string SourceName(BaseAIConsideration3D consideration)
    {
        string name = consideration.ResourceName;
        return string.IsNullOrEmpty(name) ? consideration.GetType().Name : name;
    }

    #region Test Helpers
#if TOOLS
    internal bool _TestBuffersAllocated => _interestBefore != null;
#endif
    #endregion

    /// <summary>Zero-alloc read-only window over the first <c>_count</c> pooled contributions.</summary>
    private sealed class ContributionView : IReadOnlyList<ConsiderationContribution>
    {
        private readonly DebugSteeringRecorder _r;
        public ContributionView(DebugSteeringRecorder r) => _r = r;
        public ConsiderationContribution this[int index] => _r._pool[index];
        public int Count => _r._count;

        public IEnumerator<ConsiderationContribution> GetEnumerator()
        {
            for (int i = 0; i < _r._count; i++) { yield return _r._pool[i]; }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
