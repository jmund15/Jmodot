namespace Jmodot.Implementation.Movement.Quirks;

using System.Collections.Generic;
using System.Linq;
using Core.Actors;
using Core.AI.BB;
using Core.Movement.Quirks;
using Core.Shared;
using Shared;
using GColl = Godot.Collections;

/// <summary>
/// Per-entity host for <see cref="MovementQuirk3D" /> Resources and the per-agent runtime state
/// they cannot hold themselves. Mirrors the steering processor's registry shape: an exported
/// always-on array plus refcounted runtime registration from States and BehaviorActions.
/// </summary>
[GlobalClass]
public partial class MovementQuirkProcessor3D : Node
{
    /// <summary>Always-on quirks for this entity, active for its whole life.</summary>
    [Export] private GColl.Array<MovementQuirk3D> _quirks = new();

    private readonly List<MovementQuirk3D> _runtimeQuirks = new();
    private readonly Dictionary<MovementQuirk3D, int> _refCounts = new();
    private readonly Dictionary<MovementQuirk3D, MovementQuirkRuntime> _runtimes = new();

    private IBlackboard? _bb;
    private IMovementProcessor3D _movement = null!;
    private IRng? _rng;
    private bool _warnedNoSeed;

    /// <summary>
    /// Exported quirks followed by runtime-registered ones, in insertion order. Impulses sum, so
    /// order does not change the outcome.
    /// </summary>
    public IReadOnlyList<MovementQuirk3D> ActiveQuirks { get; private set; } = new List<MovementQuirk3D>();

    public void Initialize(IBlackboard? bb, IMovementProcessor3D movement, IRng? rngOverride = null)
    {
        _bb = bb;
        _movement = movement;
        _rng = rngOverride;

        _runtimeQuirks.Clear();
        _refCounts.Clear();
        _runtimes.Clear();

        var rng = ResolveRng();
        foreach (var quirk in _quirks)
        {
            if (quirk == null) { continue; }
            _runtimes[quirk] = quirk.CreateRuntime(_bb, rng);
        }

        RebuildActiveQuirks();
    }

    public void RegisterQuirk(MovementQuirk3D quirk)
    {
        if (quirk == null) { return; }

        _refCounts.TryGetValue(quirk, out int count);
        _refCounts[quirk] = count + 1;
        if (count > 0) { return; }

        _runtimeQuirks.Add(quirk);
        if (!_runtimes.ContainsKey(quirk))
        {
            _runtimes[quirk] = quirk.CreateRuntime(_bb, ResolveRng());
        }
        RebuildActiveQuirks();
    }

    /// <summary>
    /// Drops one reference. A quirk with no reference returns silently — a BehaviorTask's Exit()
    /// fires OnExit() even when Enter() bailed on a failed condition without ever registering.
    /// The runtime is deliberately kept: state flapping between two states carrying the same quirk
    /// would otherwise reset its timers before they ever elapse.
    /// </summary>
    public void UnregisterQuirk(MovementQuirk3D quirk)
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

    public void ProcessQuirks(Vector3 desiredDirection, Vector3 agentVelocity, float delta)
    {
        if (_movement == null) { return; }

        var ctx = new MovementQuirkContext3D(_movement, desiredDirection, agentVelocity);
        // Indexed rather than foreach: ActiveQuirks is typed as the interface, so foreach would box
        // the List enumerator once per agent per physics frame on an otherwise allocation-free path.
        for (int i = 0; i < ActiveQuirks.Count; i++)
        {
            var quirk = ActiveQuirks[i];
            if (quirk == null || !_runtimes.TryGetValue(quirk, out var runtime)) { continue; }
            quirk.Tick(runtime, in ctx, delta);
        }
    }

    /// <summary>
    /// Resolving here rather than only in Initialize keeps registration order-independent: a State's
    /// OnEnter can register a quirk before the entity has wired its processor up.
    /// </summary>
    private IRng ResolveRng()
        => _rng ??= EntityRngResolver.Resolve(_bb, SeedKinds.MovementQuirk, this, ref _warnedNoSeed);

    /// <summary>
    /// Distinct is load-bearing: one quirk must tick once however many owners hold it. The refcount
    /// enforces that among runtime registrations, but the exported array bypasses it — a quirk that
    /// is both always-on and state-scoped, or listed twice in the array, would otherwise tick twice
    /// per frame against a single shared runtime and silently run at double rate.
    /// </summary>
    private void RebuildActiveQuirks()
    {
        ActiveQuirks = _quirks.Concat(_runtimeQuirks).Distinct().ToList();
    }

    #region Test Helpers
#if TOOLS
    internal void SetQuirks(GColl.Array<MovementQuirk3D> quirks) => _quirks = quirks;
#endif
    #endregion
}
