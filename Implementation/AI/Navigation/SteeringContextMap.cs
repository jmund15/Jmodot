namespace Jmodot.Implementation.AI.Navigation;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Per-frame dual-channel steering container. Considerations route their bounded [-1,1]
/// scores into three bin-indexed channels: <see cref="Interest"/> (≥0, additive),
/// <see cref="Danger"/> (≥0, additive), and <see cref="HardMask"/> (OR of Hard-constraint
/// exclusions — a bool channel that never travels in float space). <see cref="Bins"/> is the
/// owning set's ordered ring (<c>DirectionSet3D.OrderedDirections</c>), so channel index i
/// corresponds to bin i. Aggregate flags are computed on read (O(bins) scan — provably correct
/// beats incremental bookkeeping). Processor-owned; reset each frame via <see cref="Clear"/>.
/// </summary>
public sealed class SteeringContextMap
{
    public IReadOnlyList<Vector3> Bins { get; }
    public float[] Interest { get; }
    public float[] Danger { get; }
    public bool[] HardMask { get; }

    /// <summary>Mirrors the owning <c>DirectionSet3D.HasCircularOrder</c> the bins were built from —
    /// synthesis strategies gate neighbor interpolation on it (a non-planar/short ring degrades to
    /// raw-winner selection). The strategy only receives the map, so the flag rides on the map.</summary>
    public bool HasCircularOrder { get; }

    /// <summary>Bare-bins constructor; assumes a circular ring (the common case for the built-in
    /// Dir4/8/16 sets). The processor uses the two-arg form with the set's actual flag.</summary>
    public SteeringContextMap(IReadOnlyList<Vector3> bins) : this(bins, true) { }

    public SteeringContextMap(IReadOnlyList<Vector3> bins, bool hasCircularOrder)
    {
        this.Bins = bins;
        this.Interest = new float[bins.Count];
        this.Danger = new float[bins.Count];
        this.HardMask = new bool[bins.Count];
        this.HasCircularOrder = hasCircularOrder;
    }

    /// <summary>Zeroes every channel — the per-frame reset before considerations re-populate.</summary>
    public void Clear()
    {
        Array.Clear(this.Interest, 0, this.Interest.Length);
        Array.Clear(this.Danger, 0, this.Danger.Length);
        Array.Clear(this.HardMask, 0, this.HardMask.Length);
    }

    public float EffectiveScore(int bin, float dangerScale)
        => this.Interest[bin] - dangerScale * this.Danger[bin];

    /// <summary>Index of the highest-<see cref="EffectiveScore"/> non-masked bin, or -1 when every bin
    /// is Hard-masked. The single source of unmasked-argmax ranking — live synthesis and debug
    /// attribution both call this so their chosen-bin readouts never diverge under a tuned DangerScale.</summary>
    public int ArgmaxUnmasked(float dangerScale)
    {
        int best = -1;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < this.HardMask.Length; i++)
        {
            if (this.HardMask[i]) { continue; }
            float score = EffectiveScore(i, dangerScale);
            if (score > bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    /// <summary>Up to <paramref name="n"/> non-masked bin indices ranked by <see cref="EffectiveScore"/>,
    /// highest first. Empty when <paramref name="n"/> ≤ 0 or every bin is masked.</summary>
    public IReadOnlyList<int> RankUnmasked(float dangerScale, int n)
    {
        if (n <= 0) { return Array.Empty<int>(); }
        var indices = new List<int>(this.HardMask.Length);
        for (int i = 0; i < this.HardMask.Length; i++)
        {
            if (!this.HardMask[i]) { indices.Add(i); }
        }
        indices.Sort((a, b) => EffectiveScore(b, dangerScale).CompareTo(EffectiveScore(a, dangerScale)));
        if (indices.Count > n) { indices.RemoveRange(n, indices.Count - n); }
        return indices;
    }

    /// <summary>True when every bin is Hard-masked (synthesis must fall back to least-danger).</summary>
    public bool AllMasked
    {
        get
        {
            for (int i = 0; i < this.HardMask.Length; i++)
            {
                if (!this.HardMask[i]) { return false; }
            }
            return true;
        }
    }

    /// <summary>True when any bin carries positive interest (else synthesis yields Vector3.Zero).</summary>
    public bool HasAnyInterest
    {
        get
        {
            for (int i = 0; i < this.Interest.Length; i++)
            {
                if (this.Interest[i] > 0f) { return true; }
            }
            return false;
        }
    }
}
