 using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Status;

namespace Jmodot.Implementation.Combat.Status;

using System.Collections.Generic;
using System.Linq;
using AI.BB;
using Core.Combat.Reactions;
using Core.Combat.Status;
using Core.Stats;
using Core.Visual.Effects;
using Implementation.Stats;
using Implementation.Visual.Effects;
using Shared;

/// <summary>
/// Base class for all runtime status logic.
/// These are Nodes that exist as children of the StatusEffectComponent.
/// They now implement ICombatEffect directly to allow Factories to return them.
/// </summary>
public abstract partial class StatusRunner : Node
{
    // Fired when this runner is done (for any reason).
    // The StatusComponent listens to this to clean up.
    // wasDispelled: true = manually stopped/dispelled, false = completed naturally
    public event Action<StatusRunner, bool> OnStatusFinished = delegate { };

    /// <summary>
    /// Tags associated with this status (e.g., "Stun", "Poison", "Buff").
    /// Used by the StatusEffectComponent to track active states.
    /// </summary>
    public IEnumerable<CombatTag> Tags { get; protected set; } = [];
    /// <summary>
    /// Optional visual scene to spawn and hold for the duration of the status.
    /// </summary>
    public PackedScene? PersistentVisuals { get; protected set; }
    
    /// <summary>
    /// Optional visual effect (tint, flash, shader) to apply to the target during the status.
    /// </summary>
    public VisualEffect? StatusVisualEffect { get; set; }

    /// <summary>
    /// Optional spread configuration. When non-null, the StatusEffectComponent's spread-evaluation
    /// timer ticks this runner: every <see cref="StatusSpreadConfig.TryEvaluate"/> call may spawn
    /// a sibling instance of the same status type on a nearby qualifying target.
    /// Spread is a base capability of every status — burn, stun, rage, etc. all opt in by Resource.
    /// </summary>
    [Export] public StatusSpreadConfig? SpreadConfig { get; set; }

    /// <summary>
    /// The spread generation of this runner. 0 = primary application; 1+ = spread-spawned generations.
    /// Bumped by the component when it spawns a spread sibling. Read by SpreadConfig.TryEvaluate
    /// for the generation-gate and falloff curve.
    /// </summary>
    public int SpreadGeneration { get; set; } = 0;

    /// <summary>
    /// The ICombatEffect snapshot that spawned this runner. Used by the spread-evaluation
    /// path in StatusEffectComponent to spawn fresh sibling runners on nearby targets — calling
    /// SourceEffect.Apply(pickedTarget, ...) reuses the resolved values (Duration, Interval, …)
    /// without needing the original spell's StatProvider.
    /// </summary>
    public ICombatEffect? SourceEffect { get; internal set; }

    /// <summary>
    /// Per-runner accumulator (seconds) advanced each frame by the StatusEffectComponent's
    /// spread driver. When it crosses <see cref="StatusSpreadConfig.EvaluationInterval"/> the
    /// driver triggers an evaluation and decrements (preserving overshoot so cadence doesn't
    /// drift on slow frames).
    /// </summary>
    public float SpreadEvalAccumulator { get; internal set; }

    /// <summary>
    /// Total spread evaluations attempted on this runner. Compared against
    /// <see cref="StatusSpreadConfig.MaxEvaluations"/> via <see cref="StatusSpreadConfig.CanEvaluate"/>
    /// to short-circuit the loop once the budget is exhausted.
    /// </summary>
    public int SpreadEvaluationCount { get; internal set; }

    /// <summary>
    /// Stat modifiers applied to the target's StatController while this status is active.
    /// Cleaned up declaratively via <c>RemoveAllModifiersFromSource(this)</c> on Stop.
    /// Surfaced as a Resource list so non-Node runners (factories) can author them.
    /// </summary>
    public Godot.Collections.Array<StatModifier> ActiveStatModifiers { get; set; } = new();

    private bool _stopped;
    private bool _modifiersApplied;
    private Node? _visualInstance;
    protected VisualEffectController? VisualController { get; private set; }

    /// <summary>
    /// HitContext captured at <see cref="Start"/> time. Public read for the spread-evaluation loop
    /// (component reuses it when re-applying SourceEffect on a picked target).
    /// </summary>
    public HitContext Context { get; private set; }

    /// <summary>
    /// Target combatant captured at <see cref="Start"/> time. Public read for the spread loop
    /// (component reads target.OwnerNode for the spatial query origin).
    /// </summary>
    public ICombatant Target { get; private set; }

    /// <summary>
    /// Root lineage seed for this runner's spread rolls. For a primary application it derives from the
    /// applying hit (<c>HitContext.HitSeed</c>) at <see cref="Start"/>; for a spread child it is stamped
    /// via the generation chain before Start (<see cref="InjectStreamSeed"/>). Null = unseeded.
    /// </summary>
    public int? StreamSeed { get; private set; }

    private Shared.SeedSequence? _spreadRollSeq;

    /// <summary>
    /// Per-runner roll source for one spread evaluation: draws a fresh seed from this runner's spread
    /// sequence (advancing it) and returns a JmoRng seeded from it — so each evaluation is deterministic
    /// AND disjoint from sibling runners sharing the same <c>StatusSpreadConfig</c> Resource (the
    /// cross-stomp the old per-config <c>_rng</c> caused). Unseeded runners fall back to UnseededByDesign
    /// (silent — a hit-applied status is already covered by the hurtbox's ResolveHitSeed warn).
    /// <c>internal</c> because <c>StatusSpreadConfig.TryEvaluate</c> is the only caller; one call per
    /// evaluation advances the sequence, so a second call would silently skew the next roll.
    /// </summary>
    internal Shared.JmoRng NextSpreadEvalRng()
    {
        if (!StreamSeed.HasValue) { return Shared.JmoRng.UnseededByDesign(); }
        // Roll stream keyed SeedKinds.StatusSpread — the same key that derives this runner's StreamSeed from
        // the hit in Start (below). The generation-chain child derivation (StatusEffectComponent.
        // SpawnSpreadRunner) deliberately uses the DISTINCT SeedKinds.Spread so child stream seeds can't
        // collide with these roll draws — both hang off the same StreamSeed.
        _spreadRollSeq ??= new Shared.SeedSequence(StreamSeed.Value, Shared.SeedKinds.StatusSpread);
        return new Shared.JmoRng(_spreadRollSeq.Next());
    }

    /// <summary>Stamps the spread-child stream seed (generation chain) before <see cref="Start"/>; a null
    /// arg leaves the runner to derive its stream from the applying hit instead (primary application).</summary>
    internal void InjectStreamSeed(int? streamSeed)
    {
        if (streamSeed.HasValue) { StreamSeed = streamSeed; }
    }


    /// <summary>
    /// ICombatEffect Implementation.
    /// Cancels the effect.
    /// </summary>
    public void Cancel()
    {
        Stop(true);
    }
    public virtual void Start(ICombatant target, HitContext context)
    {
        Target = target;
        Context = context;

        // Primary application: derive the spread stream from the applying hit, unless a spread-child stream
        // seed was already injected (generation chain). "status_spread" keeps this disjoint from the
        // "crit" consumer of the same HitSeed.
        if (!StreamSeed.HasValue && context.HitSeed is int hitSeed)
        {
            StreamSeed = Shared.SeedManager.DeriveChild(hitSeed, Shared.SeedKinds.StatusSpread);
        }

        if (PersistentVisuals != null)
        {
            _visualInstance = PersistentVisuals.Instantiate();

            // TODO: add config for if visuals should be parented to the target or the status effect component
            target.OwnerNode.AddChild(_visualInstance);
        }

        // Resolve once for the lifetime of the runner — subclasses (e.g. TickStatusRunner)
        // also drive per-tick effects through the same controller, so the lookup must
        // succeed even when StatusVisualEffect is null.
        VisualController = FindVisualController(target);

        if (StatusVisualEffect != null)
        {
            VisualController?.PlayEffect(StatusVisualEffect);
        }

        ApplyStatModifiers();

        // Subclasses implement specific logic (Timers, Visuals)
    }

    private void ApplyStatModifiers()
    {
        if (_modifiersApplied) { return; }
        if (ActiveStatModifiers == null || ActiveStatModifiers.Count == 0) { return; }

        if (Target?.Blackboard == null
            || !Target.Blackboard.TryGet(BBDataSig.Stats, out StatController? stats)
            || stats == null)
        {
            JmoLogger.Warning(this,
                $"ActiveStatModifiers ({ActiveStatModifiers.Count}) authored but target has no StatController on Blackboard.");
            return;
        }

        foreach (var entry in ActiveStatModifiers)
        {
            if (entry == null || entry.Attribute == null || entry.Modifier == null)
            {
                JmoLogger.Warning(this, "Skipping null or invalid StatModifier entry.");
                continue;
            }
            if (!stats.TryAddModifier(entry.Attribute, entry.Modifier, this, out _))
            {
                JmoLogger.Warning(this,
                    $"Failed to apply status modifier on attribute '{entry.Attribute.AttributeName}'.");
            }
        }

        _modifiersApplied = true;
    }

    private void RemoveStatModifiers()
    {
        if (!_modifiersApplied) { return; }

        if (Target?.Blackboard != null
            && Target.Blackboard.TryGet(BBDataSig.Stats, out StatController? stats)
            && stats != null)
        {
            stats.RemoveAllModifiersFromSource(this);
        }

        _modifiersApplied = false;
    }

    /// <summary>
    /// Called when the status is removed or finished.
    /// </summary>
    /// <param name="wasDispelled"></param>
    public virtual void Stop(bool wasDispelled = false)
    {
        if (_stopped) { return; }
        _stopped = true;

        RemoveStatModifiers();

        if (_visualInstance != null && IsInstanceValid(_visualInstance))
        {
            _visualInstance.QueueFree();
            _visualInstance = null;
        }

        if (StatusVisualEffect != null && VisualController != null && IsInstanceValid(VisualController))
        {
            VisualController.StopEffect(StatusVisualEffect);
        }

        OnStatusFinished?.Invoke(this, wasDispelled);
        QueueFree();
    }
    
    private VisualEffectController? FindVisualController(ICombatant target)
    {
        if (target?.OwnerNode == null) { return null; }

        // Try to find in children
        var controller = target.OwnerNode.GetChildrenOfType<VisualEffectController>().FirstOrDefault();
        return controller;
    }

    #region Test Helpers
#if TOOLS
    /// <summary>
    /// Test-only: set <see cref="Target"/> without running <see cref="Start"/>'s full lifecycle
    /// (persistent-visual instantiation, stat-modifier application, visual-controller resolution).
    /// Used by spread tests that drive the post-Start state directly.
    /// </summary>
    internal void _TestSetTarget(ICombatant target) => Target = target;
#endif
    #endregion
}
