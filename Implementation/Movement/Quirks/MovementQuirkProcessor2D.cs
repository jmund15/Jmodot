namespace Jmodot.Implementation.Movement.Quirks;

using System.Collections.Generic;
using System.Linq;
using Core.Actors;
using Core.AI.BB;
using Core.Movement.Quirks;
using GColl = Godot.Collections;

/// <summary>
/// 2D mirror of <see cref="MovementQuirkProcessor3D" /> — see that type for the registry contract.
/// </summary>
[GlobalClass]
public partial class MovementQuirkProcessor2D : Node
{
    /// <summary>Always-on quirks for this entity, active for its whole life.</summary>
    [Export] private GColl.Array<MovementQuirk2D> _quirks = new();

    private readonly List<MovementQuirk2D> _runtimeQuirks = new();
    private readonly Dictionary<MovementQuirk2D, int> _refCounts = new();
    private readonly Dictionary<MovementQuirk2D, MovementQuirkRuntime> _runtimes = new();

    private IBlackboard? _bb;
    private IMovementProcessor2D _movement = null!;

    /// <summary>
    /// Exported quirks followed by runtime-registered ones, in insertion order. Impulses sum, so
    /// order does not change the outcome.
    /// </summary>
    public IReadOnlyList<MovementQuirk2D> ActiveQuirks { get; private set; } = new List<MovementQuirk2D>();

    public void Initialize(IBlackboard? bb, IMovementProcessor2D movement)
    {
        _bb = bb;
        _movement = movement;

        _runtimeQuirks.Clear();
        _refCounts.Clear();
        _runtimes.Clear();

        foreach (var quirk in _quirks)
        {
            if (quirk == null) { continue; }
            _runtimes[quirk] = quirk.CreateRuntime(_bb);
        }

        RebuildActiveQuirks();
    }

    public void RegisterQuirk(MovementQuirk2D quirk)
    {
        if (quirk == null) { return; }

        _refCounts.TryGetValue(quirk, out int count);
        _refCounts[quirk] = count + 1;
        if (count > 0) { return; }

        _runtimeQuirks.Add(quirk);
        if (!_runtimes.ContainsKey(quirk))
        {
            _runtimes[quirk] = quirk.CreateRuntime(_bb);
        }
        RebuildActiveQuirks();
    }

    /// <summary>
    /// Drops one reference. A quirk with no reference returns silently — a BehaviorTask's Exit()
    /// fires OnExit() even when Enter() bailed on a failed condition without ever registering.
    /// The runtime is deliberately kept: state flapping between two states carrying the same quirk
    /// would otherwise reset its timers before they ever elapse.
    /// </summary>
    public void UnregisterQuirk(MovementQuirk2D quirk)
    {
        if (quirk == null) { return; }
        if (!_refCounts.TryGetValue(quirk, out int count)) { return; }

        if (count > 1)
        {
            _refCounts[quirk] = count - 1;
            return;
        }

        _refCounts.Remove(quirk);
        _runtimeQuirks.Remove(quirk);
        RebuildActiveQuirks();
    }

    public void ProcessQuirks(Vector2 desiredDirection, Vector2 agentVelocity, float delta)
    {
        if (_movement == null) { return; }

        var ctx = new MovementQuirkContext2D(_movement, desiredDirection, agentVelocity);
        foreach (var quirk in ActiveQuirks)
        {
            if (quirk == null || !_runtimes.TryGetValue(quirk, out var runtime)) { continue; }
            quirk.Tick(runtime, in ctx, delta);
        }
    }

    private void RebuildActiveQuirks()
    {
        ActiveQuirks = _quirks.Concat(_runtimeQuirks).ToList();
    }

    #region Test Helpers
#if TOOLS
    internal void SetQuirks(GColl.Array<MovementQuirk2D> quirks) => _quirks = quirks;
#endif
    #endregion
}
