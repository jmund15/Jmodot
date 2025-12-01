using Godot;
using System;
using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.Combat.Status;

namespace Jmodot.Implementation.Combat;

/// <summary>
/// Manages active status effects (Runners) on an entity.
/// Acts as a container for StatusRunner nodes and a registry for active Tags.
/// </summary>
[GlobalClass]
public partial class StatusEffectComponent : Node, IComponent
{
    #region Events
    public event Action<StatusRunner> OnStatusAdded = delegate { };
    public event Action<StatusRunner> OnStatusRemoved = delegate { };
    
    /// <summary>
    /// Fired when a specific tag count goes from 0 to 1.
    /// </summary>
    public event Action<GameplayTag> OnTagStarted = delegate { };

    /// <summary>
    /// Fired when a specific tag count goes from 1 to 0.
    /// </summary>
    public event Action<GameplayTag> OnTagEnded = delegate { };
    #endregion

    #region Private State
    private readonly Dictionary<GameplayTag, int> _activeTags = new();
    private IBlackboard _blackboard;
    #endregion

    #region IComponent Implementation
    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        _blackboard = bb;
        IsInitialized = true;
        return true;
    }

    public void OnPostInitialize() { }

    public Node GetUnderlyingNode() => this;
    #endregion

    #region Public API
    public void AddStatus(StatusRunner runner)
    {
        if (!IsInitialized)
        {
            runner.QueueFree();
            return;
        }

        AddChild(runner);
        RegisterTags(runner.Tags);
        
        runner.OnStatusFinished += RemoveStatus;
        runner.Start();
        
        OnStatusAdded?.Invoke(runner);
    }

    public void RemoveStatus(StatusRunner runner)
    {
        runner.OnStatusFinished -= RemoveStatus;
        UnregisterTags(runner.Tags);
        
        // Runner handles its own QueueFree in Stop(), but we ensure it's removed from our logic here
        OnStatusRemoved?.Invoke(runner);
    }

    public bool HasTag(GameplayTag tag)
    {
        return _activeTags.TryGetValue(tag, out int count) && count > 0;
    }
    #endregion

    #region Internal Logic
    private void RegisterTags(GameplayTag[] tags)
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
                OnTagStarted?.Invoke(tag);
            }
        }
    }

    private void UnregisterTags(GameplayTag[] tags)
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
                    OnTagEnded?.Invoke(tag);
                }
            }
        }
    }
    #endregion
}
