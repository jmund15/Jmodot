using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Combat;
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
    #endregion

    #region IComponent Implementation
    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        _blackboard = bb;
        // if (!bb.TryGet<ICombatant>(BBDataSig.CombatantComponent, out _combatant))
        // {
        //     JmoLogger.Error(this, $"Combatant not found in {Name}'s blackboard");
        //     return false;
        // }
        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }
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

        StatusAdded?.Invoke(runner);
        return true;
    }

    public bool HasTag(CombatTag tag)
    {
        return _activeTags.TryGetValue(tag, out int count) && count > 0;
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
    /// </summary>
    private bool ProcessCategoryInteractions(StatusRunner incomingRunner)
    {
        if (InteractionRegistry == null)
        {
            return true;
        }

        // Get all tags from the incoming runner (CombatTag IS a Category now)
        var incomingTags = incomingRunner.Tags
            .Where(t => t != null)
            .ToList();

        if (incomingTags.Count == 0)
        {
            return true;
        }

        bool rejectIncoming = false;

        // Check each incoming tag against active runners
        foreach (var incomingTag in incomingTags)
        {
            // Get runners to affect (copy list to avoid modification during iteration)
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
}
