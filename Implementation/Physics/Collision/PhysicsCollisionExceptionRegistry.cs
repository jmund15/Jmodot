namespace Jmodot.Implementation.Physics.Collision;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Process-wide mirror of every physics <c>AddCollisionExceptionWith</c> call made through the spell
/// and pierce pipelines. The engine exception list stays authoritative for <c>MoveAndSlide</c>
/// pass-through (the engine calls are still made here), but ALL read sites consult this managed
/// mirror instead of <c>PhysicsBody3D.GetCollisionExceptions()</c>.
/// </summary>
/// <remarks>
/// <para><b>Why a managed mirror:</b> under Jolt, <c>GetCollisionExceptions()</c> runs
/// <c>body_get_object_instance_id</c> on each stored RID; a freed sibling's RID makes that call
/// <c>push_error("Parameter 'body' is null")</c> and continue — it does NOT throw, so every
/// <c>try/catch</c> guarding the read is inert. Answering membership from managed state never touches
/// the engine list, so no freed RID is ever enumerated.</para>
///
/// <para><b>Two layers:</b> a pure-CLR core keyed on raw <see cref="ulong"/> instance ids
/// (unit-testable with no engine), and a Godot body layer that computes ids, mirrors the engine call,
/// and wires symmetric <c>TreeExiting</c> cleanup. Generalizes the former
/// <c>CollisionActivationManager.PairedBodies</c> static registry.</para>
///
/// <para><b>Directionality:</b> pairs are direction-agnostic (A-B and B-A are one entry). Add sites
/// that historically wrote a single engine direction (pierce) now mirror both directions; the reverse
/// exception is inert for consumers that do not <c>MoveAndSlide</c> against the paired body.</para>
/// </remarks>
public static class PhysicsCollisionExceptionRegistry
{
    // Direction-agnostic pairs (Low, High) — identical key shape to the former PairedBodies set.
    private static readonly HashSet<(ulong Low, ulong High)> _pairs = new();

    // Reverse index: instanceId -> partner ids, for O(1) ClearFor without scanning _pairs.
    private static readonly Dictionary<ulong, HashSet<ulong>> _partners = new();

    // Wired TreeExiting cleanup per live pair, so any deterministic removal path (Remove/ClearFor)
    // resolves and unsubscribes the exact closure the Add call registered — preventing the closures
    // from accumulating on long-lived bodies across pool cycles.
    private static readonly Dictionary<(ulong Low, ulong High), Action> _cleanups = new();

    private static (ulong Low, ulong High) Key(ulong a, ulong b) => a < b ? (a, b) : (b, a);

    #region Pure-CLR core

    /// <summary>Records a direction-agnostic pair. Returns <c>false</c> if the pair was already present (dedup).</summary>
    internal static bool RecordPair(ulong a, ulong b)
    {
        if (!_pairs.Add(Key(a, b))) { return false; }
        AddPartner(a, b);
        AddPartner(b, a);
        return true;
    }

    /// <summary>Removes a pair. Returns <c>true</c> if the pair was present and removed.</summary>
    internal static bool RemovePair(ulong a, ulong b)
    {
        if (!_pairs.Remove(Key(a, b))) { return false; }
        RemovePartner(a, b);
        RemovePartner(b, a);
        return true;
    }

    /// <summary>Symmetric membership test. Allocation-free (value-tuple key + one <see cref="HashSet{T}.Contains"/>).</summary>
    internal static bool HasPair(ulong a, ulong b) => _pairs.Contains(Key(a, b));

    /// <summary>Every id currently paired with <paramref name="id"/>. Empty collection if none.</summary>
    internal static IReadOnlyCollection<ulong> PartnersOf(ulong id)
        => _partners.TryGetValue(id, out var set) ? set : Array.Empty<ulong>();

    /// <summary>Removes every pair touching <paramref name="id"/> from the managed maps only (no engine calls).</summary>
    internal static void ForgetId(ulong id)
    {
        if (!_partners.TryGetValue(id, out var partners)) { return; }

        // Snapshot — RemovePartner mutates _partners during iteration.
        foreach (var partner in new List<ulong>(partners))
        {
            _pairs.Remove(Key(id, partner));
            RemovePartner(partner, id);
        }
        _partners.Remove(id);
    }

    private static void AddPartner(ulong id, ulong partner)
    {
        if (!_partners.TryGetValue(id, out var set))
        {
            set = new HashSet<ulong>();
            _partners[id] = set;
        }
        set.Add(partner);
    }

    private static void RemovePartner(ulong id, ulong partner)
    {
        if (!_partners.TryGetValue(id, out var set)) { return; }
        set.Remove(partner);
        if (set.Count == 0) { _partners.Remove(id); }
    }

    #endregion

    #region Godot body layer

    /// <summary>
    /// Adds a mutual collision exception between two bodies and records it to the managed mirror.
    /// Dedups (a duplicate add is a no-op and does NOT stack a second cleanup subscription), mirrors
    /// the engine exception in BOTH directions, and wires symmetric <c>TreeExiting</c> cleanup:
    /// whichever body leaves the tree first removes the exception (while both RIDs are still valid),
    /// forgets the managed pair, and unsubscribes the shared handler from both sides.
    /// </summary>
    public static void Add(PhysicsBody3D a, PhysicsBody3D b)
    {
        if (!GodotObject.IsInstanceValid(a) || !GodotObject.IsInstanceValid(b)) { return; }

        ulong idA = a.GetInstanceId();
        ulong idB = b.GetInstanceId();

        // Dedup: pooled projectiles re-add the same caster exception on every spawn against the
        // long-lived caster. One live handler per active pair; after a cleanup fires (or ClearFor
        // runs), the pair is forgotten so a fresh add legitimately re-wires without accumulation.
        if (!RecordPair(idA, idB)) { return; }

        // Each body keeps its OWN exception list — a one-sided add only skips collisions initiated by
        // that body's MoveAndSlide. Add both directions so either mover skips the pair.
        a.AddCollisionExceptionWith(b);
        b.AddCollisionExceptionWith(a);

        var key = Key(idA, idB);
        Action? cleanup = null;
        cleanup = () =>
        {
            _cleanups.Remove(key);
            RemovePair(idA, idB);
            if (GodotObject.IsInstanceValid(a) && GodotObject.IsInstanceValid(b))
            {
                a.RemoveCollisionExceptionWith(b);
                b.RemoveCollisionExceptionWith(a);
            }
            if (GodotObject.IsInstanceValid(a)) { a.TreeExiting -= cleanup; }
            if (GodotObject.IsInstanceValid(b)) { b.TreeExiting -= cleanup; }
        };
        _cleanups[key] = cleanup;
        a.TreeExiting += cleanup;
        b.TreeExiting += cleanup;
    }

    /// <summary>
    /// Removes the mutual exception between two bodies — from the managed mirror, the engine list, and
    /// the wired <c>TreeExiting</c> cleanup. Idempotent: a no-op if the pair is not present.
    /// </summary>
    public static void Remove(PhysicsBody3D a, PhysicsBody3D b)
    {
        if (a == null || b == null) { return; }

        var key = Key(a.GetInstanceId(), b.GetInstanceId());
        if (_cleanups.TryGetValue(key, out var cleanup))
        {
            cleanup();
            return;
        }

        // No wired closure (pair added outside Add, or already torn down) — managed + best-effort engine removal.
        RemovePair(a.GetInstanceId(), b.GetInstanceId());
        if (GodotObject.IsInstanceValid(a) && GodotObject.IsInstanceValid(b))
        {
            a.RemoveCollisionExceptionWith(b);
            b.RemoveCollisionExceptionWith(a);
        }
    }

    /// <summary>
    /// READ CONTRACT — map-only membership test; NEVER queries the engine exception list.
    /// <b>Hot path: must stay allocation-free.</b> Fires per hit-candidate (non-continuous hitbox
    /// filtering) and per collision (pierce re-hit). Implementation is a native <c>GetInstanceId()</c>
    /// pair plus one <see cref="HasPair"/> (value-tuple key, single <c>HashSet.Contains</c>): zero heap
    /// allocation. Do NOT reintroduce LINQ, list building, or an engine query here.
    /// </summary>
    public static bool HasException(PhysicsBody3D a, PhysicsBody3D b)
    {
        if (a == null || b == null) { return false; }
        return HasPair(a.GetInstanceId(), b.GetInstanceId());
    }

    /// <summary>
    /// Removes every exception touching <paramref name="body"/> — from the managed mirror AND the engine
    /// list — while the body is still valid. Called at despawn and pool-return; this is the deterministic
    /// leak fix for long-lived casters whose engine list would otherwise accumulate stale RIDs across
    /// pool cycles. Partner engine removal is guarded by the wired closure's <c>IsInstanceValid</c> check,
    /// so a genuinely-freed partner is skipped rather than dereferenced.
    /// </summary>
    public static void ClearFor(PhysicsBody3D body)
    {
        if (body == null) { return; }

        ulong id = body.GetInstanceId();
        var partners = PartnersOf(id);
        if (partners.Count == 0) { return; }

        // Snapshot — invoking a cleanup mutates _partners.
        foreach (var partnerId in new List<ulong>(partners))
        {
            var key = Key(id, partnerId);
            if (_cleanups.TryGetValue(key, out var cleanup))
            {
                // The wired closure resolves both partner node refs, removes the engine exceptions
                // (IsInstanceValid-guarded), forgets the pair, and unsubscribes TreeExiting on both.
                cleanup();
            }
            else
            {
                RemovePair(id, partnerId);
            }
        }
    }

    #endregion

    #region Test Helpers
#if TOOLS
    internal static int _TestLivePairCount => _pairs.Count;

    internal static void _TestReset()
    {
        _pairs.Clear();
        _partners.Clear();
        _cleanups.Clear();
    }

    internal static bool _TestHasPair(PhysicsBody3D a, PhysicsBody3D b) => HasPair(a.GetInstanceId(), b.GetInstanceId());
#endif
    #endregion
}
