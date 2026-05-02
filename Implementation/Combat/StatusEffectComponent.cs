using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Core.Combat.Status;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Identification;
using Jmodot.Implementation.Combat.Status;

namespace Jmodot.Implementation.Combat;

using AI.BB;
using Shared;

/// <summary>
/// Manages active status effects (Runners) on an entity.
/// Acts as a container for StatusRunner nodes and a registry for active Tags.
/// </summary>
[GlobalClass]
public partial class StatusEffectComponent : Node, IComponent
{
    #region Events
    public event Action<StatusRunner> StatusAdded = delegate { };
    public event Action<StatusRunner, bool> StatusRemoved = delegate { };

    /// <summary>
    /// Fired when a specific tag count goes from 0 to 1.
    /// </summary>
    public event Action<CombatTag> TagStarted = delegate { };

    /// <summary>
    /// Fired when a specific tag count goes from 1 to 0.
    /// </summary>
    public event Action<CombatTag> TagEnded = delegate { };
    #endregion

    #region Configuration

    /// <summary>
    /// Optional registry of category interactions (e.g., Water cancels Fire).
    /// If null, no category interactions are processed.
    /// </summary>
    [Export] public CategoryInteractionRegistry? InteractionRegistry { get; set; }

    #endregion

    #region Private State
    private readonly Dictionary<CombatTag, int> _activeTags = new();
    private readonly List<StatusRunner> _activeRunners = new();
    private IBlackboard _blackboard = null!;

    /// <summary>
    /// Count of active runners carrying a non-null SpreadConfig. Used to gate
    /// per-frame _Process spread driving so non-spreading entities pay zero per-frame
    /// cost (most NPCs / non-burning entities). Toggled on AddStatus / HandleStatusFinished.
    /// </summary>
    private int _spreadableRunnerCount;
    #endregion

    #region IComponent Implementation
    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        _blackboard = bb;
        IsInitialized = true;
        // Default-off: re-enabled by AddStatus when a runner with SpreadConfig joins.
        SetProcess(false);
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }

    /// <summary>
    /// Production driver for spread cadence. Replaces the previous component-level Timer:
    /// each runner's <see cref="StatusRunner.SpreadEvalAccumulator"/> advances by delta, and
    /// when it crosses its config's <see cref="StatusSpreadConfig.EvaluationInterval"/> a
    /// single evaluation fires (overshoot preserved). Skipped while uninitialized so unit
    /// tests that construct the component bare aren't accidentally driven.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!IsInitialized) { return; }
        TickSpread(delta);
    }
    public event Action Initialized = delegate { };

    public Node GetUnderlyingNode() => this;
    #endregion

    #region Public API

    /// <summary>
    /// Attempts to add a status runner, respecting stack policies and category interactions.
    /// </summary>
    /// <param name="runner">The status runner to add.</param>
    /// <param name="combatant">The target combatant.</param>
    /// <param name="context">The hit context.</param>
    /// <returns>True if the status was added (or refreshed/replaced), false if rejected.</returns>
    public bool AddStatus(StatusRunner runner, ICombatant combatant, HitContext context)
    {
        if (!IsInitialized)
        {
            return false;
        }

        // Step 1: Evaluate stack policies for all tags on the incoming runner
        var policyResult = EvaluateStackPolicies(runner.Tags);

        if (!policyResult.IsAccepted && !policyResult.ShouldRefreshOldest)
        {
            // Fully rejected - don't add
            JmoLogger.Info(this, $"Status rejected by stack policy: {string.Join(", ", runner.Tags.Select(t => t?.TagId ?? "null"))}");
            return false;
        }

        if (policyResult.ShouldRefreshOldest)
        {
            // Find and refresh the oldest runner with a matching tag
            RefreshOldestMatchingRunner(runner);
            return true; // Considered successful even though we didn't add a new runner
        }

        if (policyResult.ShouldReplaceOldest)
        {
            // Remove the oldest runner with a matching tag before adding new one
            RemoveOldestMatchingRunner(runner.Tags);
        }

        // Step 2: Process category interactions (e.g., Water cancels Fire)
        if (!ProcessCategoryInteractions(runner))
        {
            JmoLogger.Info(this, $"Status rejected by category interaction: {string.Join(", ", runner.Tags.Select(t => t?.TagId ?? "null"))}");
            return false;
        }

        // Step 3: Add the runner
        AddChild(runner);
        _activeRunners.Add(runner);
        RegisterTags(runner.Tags);

        runner.OnStatusFinished += HandleStatusFinished;
        runner.Start(combatant, context);

        if (runner.SpreadConfig != null)
        {
            _spreadableRunnerCount++;
            SetProcess(true);
        }

        StatusAdded?.Invoke(runner);
        return true;
    }

    public bool HasTag(CombatTag tag)
    {
        return _activeTags.TryGetValue(tag, out int count) && count > 0;
    }

    /// <summary>
    /// Snapshot of all currently-active <see cref="CombatTag"/>s on this entity. Materialized
    /// each call so consumers (e.g., reaction resolvers) get a stable read; cheap for typical
    /// counts (a few active statuses at a time).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<CombatTag> GetActiveTags()
    {
        if (_activeTags.Count == 0)
        {
            return System.Array.Empty<CombatTag>();
        }
        var list = new List<CombatTag>(_activeTags.Count);
        foreach (var kvp in _activeTags)
        {
            if (kvp.Value > 0) { list.Add(kvp.Key); }
        }
        return list;
    }

    /// <summary>
    /// Gets the count of active instances with the specified tag.
    /// </summary>
    public int GetTagCount(CombatTag tag)
    {
        return _activeTags.TryGetValue(tag, out int count) ? count : 0;
    }

    /// <summary>
    /// Gets all active runners with the specified tag.
    /// </summary>
    public IEnumerable<StatusRunner> GetRunnersWithTag(CombatTag tag)
    {
        return _activeRunners.Where(r => r.Tags.Contains(tag));
    }

    /// <summary>
    /// Gets all active runners whose tags match or descend from the specified category.
    /// Uses hierarchical matching — a query for "Fire" matches tags like "Burn" if Burn descends from Fire.
    /// </summary>
    public IEnumerable<StatusRunner> GetRunnersWithCategory(Category category)
    {
        return _activeRunners.Where(r =>
            r.Tags.Any(t => t?.IsOrDescendsFrom(category) == true));
    }

    /// <summary>
    /// Immediately stops and removes all active status effects.
    /// Used for full entity reset (e.g., session restart).
    /// </summary>
    public void ClearAll()
    {
        var runners = _activeRunners.ToList();

        foreach (var runner in runners)
        {
            runner.Stop(wasDispelled: true);
        }

        JmoLogger.Info(this, $"Cleared {runners.Count} status effects.");
    }
    #endregion

    #region Stack Policy Evaluation

    /// <summary>
    /// Evaluates stack policies for all tags and returns the most restrictive result.
    /// </summary>
    private StackPolicyResult EvaluateStackPolicies(IEnumerable<CombatTag> tags)
    {
        var result = StackPolicyResult.Accept();

        foreach (var tag in tags)
        {
            if (tag?.StackPolicy == null)
            {
                continue;
            }

            var currentCount = GetTagCount(tag);
            var tagResult = tag.StackPolicy.Evaluate(currentCount);

            // Most restrictive wins:
            // Reject > RefreshOldest > ReplaceOldest > Accept
            if (!tagResult.IsAccepted && !tagResult.ShouldRefreshOldest && !tagResult.ShouldReplaceOldest)
            {
                // Full reject - return immediately
                return tagResult;
            }

            if (tagResult.ShouldRefreshOldest && !result.ShouldRefreshOldest)
            {
                result = tagResult;
            }
            else if (tagResult.ShouldReplaceOldest && !result.ShouldRefreshOldest && !result.ShouldReplaceOldest)
            {
                result = tagResult;
            }
        }

        return result;
    }

    private void RefreshOldestMatchingRunner(StatusRunner incomingRunner)
    {
        foreach (var tag in incomingRunner.Tags)
        {
            if (tag == null)
            {
                continue;
            }

            var oldest = GetOldestRunnerWithTag(tag);
            if (oldest is IDurationRefreshable refreshable)
            {
                refreshable.RefreshDuration(incomingRunner);
                JmoLogger.Info(this, $"Refreshed duration for {tag.TagId}");
                return;
            }
        }
    }

    private void RemoveOldestMatchingRunner(IEnumerable<CombatTag> tags)
    {
        foreach (var tag in tags)
        {
            if (tag == null)
            {
                continue;
            }

            var oldest = GetOldestRunnerWithTag(tag);
            if (oldest != null)
            {
                oldest.Stop(wasDispelled: true);
                JmoLogger.Info(this, $"Replaced oldest {tag.TagId} runner");
                return;
            }
        }
    }

    private StatusRunner? GetOldestRunnerWithTag(CombatTag tag)
    {
        // First runner added is oldest (list maintains insertion order)
        return _activeRunners.FirstOrDefault(r => r.Tags.Contains(tag));
    }

    #endregion

    #region Category Interaction Processing

    /// <summary>
    /// Processes category interactions for an incoming runner.
    /// Returns false if the incoming runner should be rejected (CancelIncoming/CancelBoth).
    /// CombatTag IS a Category now, so we query the registry with the tags directly.
    /// </summary>
    private bool ProcessCategoryInteractions(StatusRunner incomingRunner)
    {
        if (InteractionRegistry == null)
        {
            return true;
        }

        var incomingTags = incomingRunner.Tags
            .Where(t => t != null)
            .ToList();

        if (incomingTags.Count == 0)
        {
            return true;
        }

        bool rejectIncoming = false;

        foreach (var incomingTag in incomingTags)
        {
            var runnersToProcess = _activeRunners.ToList();

            foreach (var activeRunner in runnersToProcess)
            {
                foreach (var activeTag in activeRunner.Tags)
                {
                    if (activeTag == null)
                    {
                        continue;
                    }

                    var interaction = InteractionRegistry.GetInteraction(incomingTag, activeTag);
                    if (interaction == null)
                    {
                        continue;
                    }

                    ApplyInteractionEffect(interaction, activeRunner, incomingRunner, ref rejectIncoming);
                }
            }
        }

        return !rejectIncoming;
    }

    private void ApplyInteractionEffect(CategoryInteraction interaction, StatusRunner existingRunner, StatusRunner incomingRunner, ref bool rejectIncoming)
    {
        switch (interaction.Effect)
        {
            case CategoryInteractionEffect.CancelExisting:
                existingRunner.Stop(wasDispelled: true);
                JmoLogger.Info(this, "Interaction canceled existing effect");
                break;

            case CategoryInteractionEffect.ReduceDuration:
                if (existingRunner is IDurationModifiable modifiable)
                {
                    modifiable.ReduceDuration(interaction.Magnitude);
                    JmoLogger.Info(this, $"Interaction reduced duration by {interaction.Magnitude}");
                }
                break;

            case CategoryInteractionEffect.CancelBoth:
                existingRunner.Stop(wasDispelled: true);
                rejectIncoming = true;
                JmoLogger.Info(this, "Interaction canceled both incoming and existing effects");
                break;

            case CategoryInteractionEffect.Amplify:
                if (existingRunner is IAmplifiable amplifiable)
                {
                    amplifiable.Amplify(interaction.Magnitude);
                    JmoLogger.Info(this, $"Interaction amplified effect by {interaction.Magnitude}");
                }
                break;

            case CategoryInteractionEffect.CancelIncoming:
                rejectIncoming = true;
                JmoLogger.Info(this, "Interaction canceled incoming effect");
                break;

            case CategoryInteractionEffect.Transform:
                JmoLogger.Warning(this, "Transform interaction not yet implemented");
                break;
        }
    }

    #endregion

    #region Internal Logic
    private void HandleStatusFinished(StatusRunner runner, bool wasDispelled)
    {
        // Unsubscribe to prevent memory leaks
        runner.OnStatusFinished -= HandleStatusFinished;

        // Remove from active runners list
        _activeRunners.Remove(runner);

        if (runner.SpreadConfig != null && _spreadableRunnerCount > 0)
        {
            _spreadableRunnerCount--;
            if (_spreadableRunnerCount == 0) { SetProcess(false); }
        }

        UnregisterTags(runner.Tags);

        // This will notify the combatant
        StatusRemoved?.Invoke(runner, wasDispelled);

        // Note: runner.QueueFree() is called inside runner.Stop()
    }
    private void RegisterTags(IEnumerable<CombatTag> tags)
    {
        foreach (var tag in tags)
        {
            if (tag == null) { continue; }

            if (!_activeTags.ContainsKey(tag))
            {
                _activeTags[tag] = 0;
            }

            _activeTags[tag]++;

            if (_activeTags[tag] == 1)
            {
                TagStarted?.Invoke(tag);
            }
        }
    }

    private void UnregisterTags(IEnumerable<CombatTag> tags)
    {
        foreach (var tag in tags)
        {
            if (tag == null) { continue; }

            if (_activeTags.ContainsKey(tag))
            {
                _activeTags[tag]--;

                if (_activeTags[tag] <= 0)
                {
                    _activeTags.Remove(tag);
                    TagEnded?.Invoke(tag);
                }
            }
        }
    }
    #endregion

    #region Spread Evaluation

    /// <summary>
    /// Walks every active runner with a SpreadConfig and runs a single evaluation on each
    /// (regardless of cadence — for one-shot orchestration tests + non-time-driven contexts).
    /// Honors <see cref="StatusSpreadConfig.CanEvaluate"/> so the per-runner cap is enforced
    /// uniformly with the cadence-driven path.
    /// </summary>
    public void EvaluateSpread()
    {
        if (!IsInitialized) { return; }

        // ToList: SpawnSpreadRunner mutates _activeRunners (via AddStatus); iterating a snapshot
        // avoids "collection was modified" exceptions and double-evaluation of fresh siblings
        // within the same tick (their first evaluation happens on the next tick).
        foreach (var runner in _activeRunners.ToList())
        {
            EvaluateSpreadForRunner(runner);
        }
    }

    /// <summary>
    /// Cadence + cap driver. Advances each runner's accumulator by <paramref name="delta"/>
    /// and fires evaluations when the configured interval is crossed, until either the
    /// accumulator dips back below the interval or the per-runner cap is reached. Public so
    /// tests can drive deterministic time without a SceneTree.
    /// </summary>
    public void TickSpread(double delta)
    {
        if (!IsInitialized) { return; }
        if (delta <= 0) { return; }
        // Per-frame perf is gated upstream by SetProcess(false) when no runner has a
        // SpreadConfig (toggled by AddStatus / HandleStatusFinished). This public API
        // remains directly invokable from tests that bypass AddStatus and inject runners
        // through other paths — the foreach below filters per-runner via cfg==null.

        float dt = (float)delta;
        foreach (var runner in _activeRunners.ToList())
        {
            var cfg = runner.SpreadConfig;
            if (cfg == null) { continue; }
            // Defensive: an interval of 0 or negative would make the drain loop never decrement
            // and spin forever (or until MaxEvaluations clamps it). PropertyHint clamps in the
            // editor, but SetTestValues / programmatic Resource construction can bypass that.
            if (cfg.EvaluationInterval <= 0f) { continue; }

            runner.SpreadEvalAccumulator += dt;

            // Interval may be sampled multiple times per call when delta exceeds it (slow
            // frames, manual large ticks). Loop until we either drain below interval or hit cap.
            while (runner.SpreadEvalAccumulator >= cfg.EvaluationInterval
                   && cfg.CanEvaluate(runner.SpreadEvaluationCount))
            {
                runner.SpreadEvalAccumulator -= cfg.EvaluationInterval;
                EvaluateSpreadForRunner(runner);
            }

            // Cap exhausted with leftover accumulator: clamp to 0 so we don't keep growing it.
            if (!cfg.CanEvaluate(runner.SpreadEvaluationCount))
            {
                runner.SpreadEvalAccumulator = 0f;
            }
        }
    }

    /// <summary>
    /// One evaluation pass for a single runner. Honors generation gate (via TryEvaluate),
    /// chance, filtering, AND <see cref="StatusSpreadConfig.CanEvaluate"/>. Increments
    /// <see cref="StatusRunner.SpreadEvaluationCount"/> when an evaluation actually runs
    /// (regardless of whether it picks any target — the cap counts attempts, not successes).
    /// </summary>
    private void EvaluateSpreadForRunner(StatusRunner runner)
    {
        var cfg = runner.SpreadConfig;
        if (cfg == null) { return; }
        if (runner.Target == null) { return; }
        if (!cfg.CanEvaluate(runner.SpreadEvaluationCount)) { return; }

        runner.SpreadEvaluationCount++;

        var nearby = QueryNearbyTargets(runner.Target, cfg.Range, cfg.SpreadCollisionMask);
        if (!cfg.TryEvaluate(runner, nearby, out var picks)) { return; }

        int newGen = runner.SpreadGeneration + 1;
        foreach (var pickedTarget in picks)
        {
            SpawnSpreadRunner(runner, pickedTarget, newGen);
        }
    }

    /// <summary>
    /// Returns ICombatants in <paramref name="radius"/> world-units around the host target's
    /// position whose nodes either ARE or HAVE-CHILD an ICombatant. Virtual so tests can
    /// override with deterministic candidate sets without driving real physics queries.
    /// </summary>
    protected virtual IEnumerable<ICombatant> QueryNearbyTargets(ICombatant host, float radius, uint collisionMask = uint.MaxValue)
    {
        var result = new List<ICombatant>();
        if (host.OwnerNode is not Node3D origin) { return result; }
        if (!origin.IsInsideTree()) { return result; }

        var space = origin.GetWorld3D()?.DirectSpaceState;
        if (space == null) { return result; }

        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new SphereShape3D { Radius = radius },
            Transform = new Transform3D(Basis.Identity, origin.GlobalPosition),
            CollisionMask = collisionMask
        };

        var hits = space.IntersectShape(query);
        foreach (var hit in hits)
        {
            var collider = hit["collider"].As<Node3D>();
            if (collider == null || !IsInstanceValid(collider)) { continue; }
            if (collider == origin) { continue; }

            // ICombatant may live as the collider itself, or as a sibling/child component.
            ICombatant? combatant = null;
            if (collider is ICombatant c1) { combatant = c1; }
            else
            {
                foreach (var child in collider.GetChildren())
                {
                    if (child is ICombatant c2) { combatant = c2; break; }
                }
            }

            if (combatant != null && combatant != host) { result.Add(combatant); }
        }

        return result;
    }

    /// <summary>
    /// Re-Apply the source runner's <see cref="StatusRunner.SourceEffect"/> on the picked
    /// target with an incremented spread generation. SourceEffect is shared across all
    /// descendants of the original cast — save/restore SpreadGeneration around Apply so
    /// stale generation values don't leak to subsequent reads (telemetry, debug log,
    /// later iterations of the foreach in EvaluateSpread).
    /// </summary>
    private void SpawnSpreadRunner(StatusRunner sourceRunner, ICombatant pickedTarget, int newGeneration)
    {
        if (sourceRunner.SourceEffect is not ISpreadAwareCombatEffect spreadAware)
        {
            // Runner was created outside the spread-aware factory path — can't reproduce.
            return;
        }

        int previousGeneration = spreadAware.SpreadGeneration;
        spreadAware.SpreadGeneration = newGeneration;
        try
        {
            spreadAware.Apply(pickedTarget, sourceRunner.Context);
        }
        finally
        {
            spreadAware.SpreadGeneration = previousGeneration;
        }
    }

    #endregion

    #region Test Helpers
#if TOOLS
    /// <summary>
    /// Test-only: directly register tags as active without instantiating a full StatusRunner.
    /// Lets transition-condition tests assert tag-presence semantics in isolation.
    /// </summary>
    internal void _TestRegisterTags(IEnumerable<CombatTag> tags) => RegisterTags(tags);

    /// <summary>
    /// Test-only: directly unregister tags. Pairs with _TestRegisterTags for symmetric setup/teardown.
    /// </summary>
    internal void _TestUnregisterTags(IEnumerable<CombatTag> tags) => UnregisterTags(tags);
#endif
    #endregion
}
