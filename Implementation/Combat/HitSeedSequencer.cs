namespace Jmodot.Implementation.Combat;

using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Shared;

/// <summary>
/// Per-receiver hit-seed sequencer. For each incoming attack (keyed by its <c>attackSeed</c>) it
/// advances a hit counter and derives a per-hit lineage seed
/// <c>DeriveChild(attackSeed, "hit", receiverEntitySeed, hitIdx)</c>, so successive hits of one
/// attack on this receiver get distinct seeds and two victims of the same attack never collide
/// (receiver identity is a derivation segment).
/// <para>
/// COMPOSED by HurtboxComponent3D/2D (not inherited) so the 2D/3D pair share one implementation and
/// the derivation + provenance 2×2 stay pure-CLR testable (no Godot Node). Plain counter dict — a raw
/// <c>hitIdx</c>, NOT an L1 <c>SeedSequence</c>: the formula needs the two-int-segment
/// <c>DeriveChild</c> overload a single-index SeedSequence can't express, and these counters are
/// transient (cleared on the receiver's pool return), never save-serialized.
/// </para>
/// <para>
/// Cleanup: <see cref="Reset"/> on the receiver's pool return clears all entries; a recency-window-aware
/// stale sweep (soft cap <c>DefaultMaxSequences</c>) backstops entries whose attacker vanished via
/// QueueFree (which fires no domain event). On cap pressure the least-recently-touched entry is evicted
/// ONLY if it is stale - untouched for at least one full cap-window of derivations; a still-active attack
/// (touched within the window) is protected even when it is the LRU, so an in-flight continuous attack
/// between ticks is never evicted and never re-derives from idx 0 (the duplicate hit seed pure LRU could
/// produce). When every entry is within the window (many simultaneously-active attacks on one receiver),
/// the dictionary grows transiently above the cap - bounded by the count of genuinely-active distinct
/// attackers, cleared on pool return. Residual window (now far narrower): an attack untouched for
/// >= <c>DefaultMaxSequences</c> derivations while the dict is at cap can still be swept and re-derive
/// from idx 0 - reachable only under absurd distinct-attacker counts on a single receiver.
/// </para>
/// </summary>
public sealed class HitSeedSequencer
{
    private const int DefaultMaxSequences = 128;

    private struct HitSeq
    {
        public int Count;
        public long LastTouched;
    }

    private readonly Dictionary<int, HitSeq> _sequences = new();
    private readonly int _maxSequences;
    private long _tick;

    public HitSeedSequencer()
    {
        this._maxSequences = DefaultMaxSequences;
    }

    /// <summary>
    /// Resolves the per-hit seed for an incoming attack per the provenance 2×2 table:
    /// Seeded + receiver-seed-present → derive; Seeded + no receiver seed → null + warn;
    /// UnseededByDesign → null silent; Missing → null + warn. <paramref name="warnMissing"/> is set
    /// when the caller should fire a one-shot missing-seed warning (the caller owns the latch).
    /// </summary>
    public int? ResolveHitSeed(int? attackSeed, SeedProvenance provenance, int? receiverEntitySeed, out bool warnMissing)
    {
        warnMissing = false;
        switch (provenance)
        {
            case SeedProvenance.Seeded when attackSeed.HasValue:
                if (receiverEntitySeed.HasValue)
                {
                    int idx = this.NextHitIdx(attackSeed.Value);
                    return SeedManager.DeriveChild(attackSeed.Value, SeedKinds.Hit, receiverEntitySeed.Value, idx);
                }
                warnMissing = true;
                return null;

            case SeedProvenance.UnseededByDesign:
                return null;

            default: // Missing, or Seeded with a null AttackSeed (treated as Missing)
                warnMissing = true;
                return null;
        }
    }

    /// <summary>Clears all per-attack counters — call on the receiver's pool return/re-acquisition.</summary>
    public void Reset() => this._sequences.Clear();

    private int NextHitIdx(int attackSeed)
    {
        if (!this._sequences.TryGetValue(attackSeed, out var seq))
        {
            if (this._sequences.Count >= this._maxSequences) { this.TryEvictStaleEntry(); }
            seq = default;
        }

        int idx = seq.Count;
        seq.Count = idx + 1;
        seq.LastTouched = ++this._tick;
        this._sequences[attackSeed] = seq;
        return idx;
    }

    // Recency-window-aware eviction (replaces pure LRU): evict the least-recently-touched entry ONLY if it
    // is stale — untouched for at least one full cap-window of derivations (_maxSequences). A still-active
    // attack (touched within the window) is protected even when it is the LRU, so an in-flight continuous
    // attack between ticks is never evicted → never re-derives from idx 0 (the duplicate hit seed). When
    // every entry is within the window, no eviction occurs and the dict grows transiently (bounded by the
    // count of genuinely-active distinct attackers, cleared on pool return).
    private void TryEvictStaleEntry()
    {
        int oldestKey = 0;
        long oldest = long.MaxValue;
        foreach (var kvp in this._sequences)
        {
            if (kvp.Value.LastTouched < oldest)
            {
                oldest = kvp.Value.LastTouched;
                oldestKey = kvp.Key;
            }
        }
        if (this._tick - oldest >= this._maxSequences)
        {
            this._sequences.Remove(oldestKey);
        }
    }

    #region Test Helpers
#if TOOLS
    /// <summary>Test-only: construct with a small cap so the eviction/recency policy is exercisable
    /// in a handful of calls (production always uses the parameterless ctor → DefaultMaxSequences).</summary>
    internal HitSeedSequencer(int maxSequences)
    {
        this._maxSequences = maxSequences;
    }

    internal int SequenceCount => this._sequences.Count;
#endif
    #endregion
}
