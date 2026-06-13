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
/// Cleanup: <see cref="Reset"/> on the receiver's pool return clears all entries; a bounded
/// least-recently-used eviction backstops entries whose attacker vanished via QueueFree (which fires
/// no domain event). LRU never evicts a just-active attack, so an overflow cannot reset an in-flight
/// counter (which would duplicate a hit seed).
/// </para>
/// </summary>
public sealed class HitSeedSequencer
{
    private const int MaxSequences = 128;

    private struct HitSeq
    {
        public int Count;
        public long LastTouched;
    }

    private readonly Dictionary<int, HitSeq> _sequences = new();
    private long _tick;

    /// <summary>
    /// Resolves the per-hit seed for an incoming attack per the provenance 2×2 table:
    /// Seeded + receiver-seed-present → derive; Seeded + no receiver seed → null + warn;
    /// UnseededByDesign → null silent; Missing → null + warn. <paramref name="warnMissing"/> is set
    /// when the caller should fire a one-shot missing-seed warning (the caller owns the latch).
    /// </summary>
    public int? Resolve(int? attackSeed, SeedProvenance provenance, int? receiverEntitySeed, out bool warnMissing)
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
            if (this._sequences.Count >= MaxSequences) { this.EvictLeastRecentlyUsed(); }
            seq = default;
        }

        int idx = seq.Count;
        seq.Count = idx + 1;
        seq.LastTouched = ++this._tick;
        this._sequences[attackSeed] = seq;
        return idx;
    }

    private void EvictLeastRecentlyUsed()
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
        this._sequences.Remove(oldestKey);
    }

    #region Test Helpers
#if TOOLS
    internal int SequenceCount => this._sequences.Count;
#endif
    #endregion
}
