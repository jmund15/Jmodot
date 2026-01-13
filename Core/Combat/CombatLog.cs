namespace Jmodot.Core.Combat;

using Godot;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Shared;
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
    private readonly Dictionary<ulong, List<CombatResult>> _resultsByFrame = new(); // ACCURATE
    private readonly Dictionary<float, List<CombatResult>> _resultsByCombatTime = new(); // for time based calcs

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

        // DIAGNOSTIC: Log when result is added
        JmoLogger.Info(this, $"[DIAG] CombatLog.Log: {result.GetType().Name} at frame {currTimeContext.FrameId}");

        if (!_resultsByFrame.ContainsKey(currTimeContext.FrameId))
        {
            _resultsByFrame[currTimeContext.FrameId] = new List<CombatResult> { result };
        }
        else { _resultsByFrame[currTimeContext.FrameId].Add(result); }

        if (!_resultsByCombatTime.ContainsKey(currTimeContext.CombatTimeMarker))
        {
            _resultsByCombatTime[currTimeContext.CombatTimeMarker] = new List<CombatResult> { result };
        }
        else { _resultsByCombatTime[currTimeContext.CombatTimeMarker].Add(result); }
    }

    /// <summary>
    /// Returns all results of a specific type that occurred THIS frame.
    /// </summary>
    public IEnumerable<T> GetEvents<T>() where T : CombatResult
    {
        var currTimeContext = GetCurrentTimeContext();
        //GD.Print($"searching result at frame '{currTimeContext.FrameId}', combat time: {currTimeContext.CombatTimeMarker}");

        if (_resultsByFrame.TryGetValue(currTimeContext.FrameId, out var results))
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
        var currTimeContext = GetCurrentTimeContext();

        if (!_resultsByFrame.TryGetValue(currTimeContext.FrameId, out var results))
        {
            return false;
        }

        var query = results.Where(e => e is T).Cast<T>();

        if (predicate != null)
        {
            return query.Any(e => predicate(e));
        }
        return query.Any();
    }

    // Optional: Call this periodically to prevent the list from growing infinitely
    // if the AI stops querying it.
    public void PruneAllButCurrentFrame()
    {
        var currTimeContext = GetCurrentTimeContext();

        if (!_resultsByFrame.TryGetValue(currTimeContext.FrameId, out var currList))
        {
            _resultsByFrame.Clear();
            return;
        }

        _resultsByFrame.Clear();
        _resultsByFrame[currTimeContext.FrameId] = currList;

        _resultsByCombatTime.Clear();
        _resultsByCombatTime[currTimeContext.CombatTimeMarker] = currList;
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
        var allowedCtxs = _resultsByFrame.Keys.Where(ctx => ctx >= frameCutoff);
        var allResults =
            _resultsByFrame.Where(pair => allowedCtxs.Contains(pair.Key)).Select(pair => pair.Value);
        return allResults.SelectMany(x => x).OfType<T>();
    }

    public IEnumerable<T> GetAllCombatResultsWithinCombatTime<T>(float combatTimeThreshold) where T : CombatResult
    {
        var combatTimeCutoff = CombatTimeElapsed - combatTimeThreshold;
        var allowedCtxs = _resultsByCombatTime.Keys.Where(ctx => ctx >= combatTimeCutoff);
        var allResults =
            _resultsByCombatTime.Where(pair => allowedCtxs.Contains(pair.Key)).Select(pair => pair.Value);
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
