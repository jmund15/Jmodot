namespace Jmodot.Core.Combat;

using Godot;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Combat;
using Reactions;

/// <summary>
/// A transient buffer that stores CombatResults for exactly one physics frame.
/// Allows the HSM to query "Did X happen this frame?" without race conditions.
/// </summary>
public class CombatLog
{
    private record struct ResultTimeContext
    {
        public ulong FrameId;
        public float CombatTimeMarker;
    }
    public float CombatTimeElapsed { get; private set; } = 0f;
    private readonly Dictionary<ResultTimeContext, List<CombatResult>> _resultsByTime = new();

    public void UpdateCombatTime(float combatDelta)
    {
        CombatTimeElapsed += combatDelta;
    }

    /// <summary>
    /// Adds a result to the log stamped with the current frame.
    /// </summary>
    public void Log(CombatResult result)
    {
        var currTimeContext = GetCurrentTimeContext();
        if (!_resultsByTime.ContainsKey(currTimeContext))
        {
            _resultsByTime[currTimeContext] = new List<CombatResult> { result };
        }
        else
        {
            _resultsByTime[currTimeContext].Add(result);
        }
    }

    /// <summary>
    /// Returns all results of a specific type that occurred THIS frame.
    /// </summary>
    public IEnumerable<T> GetEvents<T>() where T : CombatResult
    {
        var currTimeContext = GetCurrentTimeContext();

        // We iterate backwards to find recent events, but return them in order if needed.
        // Also cleans up old events implicitly or explicit cleanup can be done separately.
        return _resultsByTime[currTimeContext].OfType<T>();
    }

    /// <summary>
    /// Checks if a specific condition occurred this frame.
    /// </summary>
    public bool HasEvent<T>(System.Func<T, bool> predicate = null) where T : CombatResult
    {
        var currTimeContext = GetCurrentTimeContext();
        var query = _resultsByTime[currTimeContext].Where(e => e is T);

        if (predicate != null)
        {
            return query.Any(e => predicate(e as T));
        }
        return query.Any();
    }

    // Optional: Call this periodically to prevent the list from growing infinitely
    // if the AI stops querying it.
    public void PruneAllButCurrentFrame()
    {
        var currTimeContext = GetCurrentTimeContext();
        var currList = _resultsByTime[currTimeContext];
        _resultsByTime.Clear();
        _resultsByTime[currTimeContext] = currList;
        // TODO: check if there's a more efficient way?
    }

    public void PruneOldEventsByFrameCutoff(ulong frameCutoff)
    {
        // TODO: fill out
    }
    public void PruneOldEventsByCombatTimeCutoff(float combatTimeCutoff)
    {
        // TODO: fill out
    }

    public IEnumerable<T> GetAllCombatResultsWithinLastFrameAmount<T>(ulong frameAmt) where T : CombatResult
    {
        var frameCutoff = Engine.GetPhysicsFrames() - frameAmt;
        var allowedCtxs = _resultsByTime.Keys.Where(ctx => ctx.FrameId >= frameCutoff);
        var allResults =
            _resultsByTime.Where(pair => allowedCtxs.Contains(pair.Key)).Select(pair => pair.Value);
        return allResults.SelectMany(x => x).OfType<T>();
    }

    public IEnumerable<T> GetAllCombatResultsWithinCombatTime<T>(float combatTimeThreshold) where T : CombatResult
    {
        var combatTimeCutoff = CombatTimeElapsed - combatTimeThreshold;
        var allowedCtxs = _resultsByTime.Keys.Where(ctx => ctx.CombatTimeMarker >= combatTimeCutoff);
        var allResults =
            _resultsByTime.Where(pair => allowedCtxs.Contains(pair.Key)).Select(pair => pair.Value);
        return allResults.SelectMany(x => x).OfType<T>();
    }

    #region Helpers

    private ResultTimeContext GetCurrentTimeContext()
    {
        return new ResultTimeContext()
        {
            FrameId = Engine.GetPhysicsFrames(),
            CombatTimeMarker = CombatTimeElapsed
        };
    }
    #endregion
}
