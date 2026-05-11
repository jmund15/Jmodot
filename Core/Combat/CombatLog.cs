namespace Jmodot.Core.Combat;

using Godot;
using System.Collections.Generic;
using System.Linq;
using Reactions;

/// <summary>
/// A transient buffer that stores CombatResults stamped with the physics frame and
/// combat-time elapsed when they occurred. Allows the HSM and damage layer to query
/// "Did X happen this frame?" or "What just caused this? (within N seconds)" without
/// race conditions.
/// </summary>
/// <remarks>
/// Time-windowed queries (<see cref="GetMostRecent{T}"/>,
/// <see cref="GetAllCombatResultsWithinCombatTime{T}"/>) walk a <c>SortedList</c> of
/// (combatTime, results) pairs in order — no LINQ chains, no per-call iterator
/// allocations. The float key is safe here because <c>CombatTimeElapsed</c> is
/// monotonically incremented by <see cref="UpdateCombatTime"/>; same-frame logs reuse
/// the same bit-identical key (no hash collisions), cross-frame logs are uniquely
/// ordered by construction.
/// </remarks>
public class CombatLog
{
    public float CombatTimeElapsed { get; private set; } = 0f;
    private readonly Dictionary<ulong, List<CombatResult>> _resultsByFrame = new();
    private readonly SortedList<float, List<CombatResult>> _resultsByCombatTime = new();

    public void UpdateCombatTime(float combatDelta)
    {
        CombatTimeElapsed += combatDelta;
    }

    /// <summary>
    /// Adds a result to the log stamped with the current frame and combat time.
    /// </summary>
    public void Log(CombatResult result)
    {
        var frameId = Engine.GetPhysicsFrames();

        if (!_resultsByFrame.TryGetValue(frameId, out var frameList))
        {
            frameList = new List<CombatResult>();
            _resultsByFrame[frameId] = frameList;
        }
        frameList.Add(result);

        if (!_resultsByCombatTime.TryGetValue(CombatTimeElapsed, out var timeList))
        {
            timeList = new List<CombatResult>();
            _resultsByCombatTime.Add(CombatTimeElapsed, timeList);
        }
        timeList.Add(result);
    }

    /// <summary>
    /// Returns all results of a specific type that occurred THIS frame.
    /// </summary>
    public IEnumerable<T> GetEvents<T>() where T : CombatResult
    {
        var frameId = Engine.GetPhysicsFrames();
        if (_resultsByFrame.TryGetValue(frameId, out var results))
        {
            return results.OfType<T>();
        }
        return Enumerable.Empty<T>();
    }

    /// <summary>
    /// Checks if a specific condition occurred this frame.
    /// </summary>
    public bool HasEvent<T>(System.Func<T, bool>? predicate = null) where T : CombatResult
    {
        var frameId = Engine.GetPhysicsFrames();
        if (!_resultsByFrame.TryGetValue(frameId, out var results))
        {
            return false;
        }

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i] is T match && (predicate == null || predicate(match)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Clears all frames except the current one. Call periodically to prevent unbounded
    /// growth when consumers stop querying.
    /// </summary>
    public void PruneAllButCurrentFrame()
    {
        var frameId = Engine.GetPhysicsFrames();
        if (!_resultsByFrame.TryGetValue(frameId, out var currList))
        {
            _resultsByFrame.Clear();
            _resultsByCombatTime.Clear();
            return;
        }

        _resultsByFrame.Clear();
        _resultsByFrame[frameId] = currList;

        _resultsByCombatTime.Clear();
        _resultsByCombatTime.Add(CombatTimeElapsed, currList);
    }

    public void PruneOldEventsByFrameCutoff(ulong frameCutoff)
    {
        var keysToRemove = _resultsByFrame.Keys.Where(k => k < frameCutoff).ToList();
        foreach (var key in keysToRemove)
        {
            _resultsByFrame.Remove(key);
        }
    }

    /// <summary>
    /// Removes all by-time entries strictly older than <paramref name="combatTimeCutoff"/>.
    /// Uses SortedList's index ordering to prune from the front in O(prunable count) without
    /// LINQ allocations.
    /// </summary>
    public void PruneOldEventsByCombatTimeCutoff(float combatTimeCutoff)
    {
        while (_resultsByCombatTime.Count > 0 && _resultsByCombatTime.Keys[0] < combatTimeCutoff)
        {
            _resultsByCombatTime.RemoveAt(0);
        }
    }

    public IEnumerable<T> GetAllCombatResultsWithinLastFrameAmount<T>(ulong frameAmt) where T : CombatResult
    {
        var frameCutoff = Engine.GetPhysicsFrames() - frameAmt;
        foreach (var pair in _resultsByFrame)
        {
            if (pair.Key < frameCutoff)
            {
                continue;
            }
            var list = pair.Value;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is T match)
                {
                    yield return match;
                }
            }
        }
    }

    /// <summary>
    /// All results of type T within the last <paramref name="combatTimeThreshold"/> seconds.
    /// Iterates the SortedList in ascending order from the cutoff; yields one match at a time
    /// with zero LINQ chains and zero per-call iterator allocations.
    /// </summary>
    public IEnumerable<T> GetAllCombatResultsWithinCombatTime<T>(float combatTimeThreshold) where T : CombatResult
    {
        var combatTimeCutoff = CombatTimeElapsed - combatTimeThreshold;
        var keys = _resultsByCombatTime.Keys;
        var values = _resultsByCombatTime.Values;

        for (int k = 0; k < keys.Count; k++)
        {
            if (keys[k] < combatTimeCutoff)
            {
                continue;
            }
            var list = values[k];
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is T match)
                {
                    yield return match;
                }
            }
        }
    }

    /// <summary>
    /// Most recently logged combat result of type T within the given seconds window, or null.
    /// The canonical "what just caused this?" lookup — damage source attribution, kill credit,
    /// post-hoc DoT attribution.
    /// </summary>
    /// <remarks>
    /// Walks the SortedList by index descending from the most-recent entry; breaks as soon
    /// as the cutoff is crossed. Zero allocations, zero LINQ — this is on the per-impact
    /// hot path for <c>ForceImpactDamageApplier</c>.
    /// </remarks>
    public T? GetMostRecent<T>(float withinSeconds) where T : CombatResult
    {
        var combatTimeCutoff = CombatTimeElapsed - withinSeconds;
        var keys = _resultsByCombatTime.Keys;
        var values = _resultsByCombatTime.Values;

        for (int k = keys.Count - 1; k >= 0; k--)
        {
            if (keys[k] < combatTimeCutoff)
            {
                break;
            }
            var list = values[k];
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is T match)
                {
                    return match;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Returns true if any <typeparamref name="T"/> within the last
    /// <paramref name="combatTimeThreshold"/> seconds satisfies the optional
    /// <paramref name="predicate"/>. Window-aware sibling to <see cref="HasEvent{T}"/>.
    /// </summary>
    /// <remarks>
    /// Walks the SortedList descending from most-recent and early-exits on the first match;
    /// breaks as soon as the cutoff is crossed. Zero LINQ, zero per-call iterator allocations.
    /// Hot-path for per-frame BTConditions (RecentHitCondition gating sequences).
    /// </remarks>
    public bool AnyWithinCombatTime<T>(float combatTimeThreshold, System.Func<T, bool>? predicate = null) where T : CombatResult
    {
        var combatTimeCutoff = CombatTimeElapsed - combatTimeThreshold;
        var keys = _resultsByCombatTime.Keys;
        var values = _resultsByCombatTime.Values;

        for (int k = keys.Count - 1; k >= 0; k--)
        {
            if (keys[k] < combatTimeCutoff)
            {
                break;
            }
            var list = values[k];
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is T match && (predicate == null || predicate(match)))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
