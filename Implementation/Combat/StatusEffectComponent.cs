using Godot;
using System;
using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.AI.BB; // For BBDataSig if needed, though we use strings for keys mostly

namespace Jmodot.Implementation.Combat;

/// <summary>
/// Manages active status effects (DoTs, Buffs, Debuffs) on an entity.
/// Ticks them every frame and handles their lifecycle.
/// </summary>
[GlobalClass]
public partial class StatusEffectComponent : Node, IComponent
{
    #region Events
    public event Action<ActiveStatus> OnStatusAdded = delegate { };
    public event Action<ActiveStatus> OnStatusRemoved = delegate { };
    
    // Specific signals for common states that other systems (HSM) might need to know about immediately
    public event Action OnStunned = delegate { };
    public event Action OnStunCleared = delegate { };
    #endregion

    #region Private State
    private readonly List<ActiveStatus> _activeStatuses = new();
    private readonly List<ActiveStatus> _statusesToRemove = new();
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

    #region Godot Lifecycle
    public override void _Process(double delta)
    {
        if (!IsInitialized) return;

        // Tick all active statuses
        foreach (var status in _activeStatuses)
        {
            status.OnTick(delta);
            if (status.IsFinished)
            {
                _statusesToRemove.Add(status);
            }
        }

        // Cleanup finished statuses
        if (_statusesToRemove.Count > 0)
        {
            foreach (var status in _statusesToRemove)
            {
                RemoveStatus(status);
            }
            _statusesToRemove.Clear();
        }
    }
    #endregion

    #region Public API
    public void AddStatus(ActiveStatus status)
    {
        if (!IsInitialized) return;

        _activeStatuses.Add(status);
        status.OnApply();
        OnStatusAdded?.Invoke(status);

        // Check for specific flags to emit convenience signals
        // In a more complex system, this could be data-driven via tags
        if (status is Status.StunStatus)
        {
            OnStunned?.Invoke();
        }
    }

    public void RemoveStatus(ActiveStatus status)
    {
        if (_activeStatuses.Remove(status))
        {
            status.OnRemove();
            OnStatusRemoved?.Invoke(status);

             if (status is Status.StunStatus)
            {
                // Check if any other stuns remain
                if (!HasStatus<Status.StunStatus>())
                {
                    OnStunCleared?.Invoke();
                }
            }
        }
    }

    public bool HasStatus<T>() where T : ActiveStatus
    {
        foreach (var status in _activeStatuses)
        {
            if (status is T) return true;
        }
        return false;
    }
    
    public T? GetStatus<T>() where T : ActiveStatus
    {
         foreach (var status in _activeStatuses)
        {
            if (status is T tStatus) return tStatus;
        }
        return null;
    }
    #endregion
}
