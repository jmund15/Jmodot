using Godot;
using System;
using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
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

    #region Private State
    // TODO: why is the int relative to amount of tags? where is priority here?
    private readonly Dictionary<CombatTag, int> _activeTags = new();
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
        Initialized?.Invoke();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }
    public event Action? Initialized;

    public Node GetUnderlyingNode() => this;
    #endregion

    #region Public API
    public bool AddStatus(StatusRunner runner, ICombatant combatant, HitContext context)
    {
        if (!IsInitialized)
        {
            return false;
        }

        AddChild(runner);
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
    #endregion

    #region Internal Logic
    private void HandleStatusFinished(StatusRunner runner, bool wasDispelled)
    {
        // Unsubscribe to prevent memory leaks
        runner.OnStatusFinished -= HandleStatusFinished;

        UnregisterTags(runner.Tags);

        // This will notify the combatant
        StatusRemoved?.Invoke(runner, wasDispelled);

        // Note: runner.QueueFree() is called inside runner.Stop()
    }
    private void RegisterTags(IEnumerable<CombatTag> tags)
    {
        foreach (var tag in tags)
        {
            if (tag == null) continue;

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
            if (tag == null) continue;

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
