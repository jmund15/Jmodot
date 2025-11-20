using Godot;
using System;
using System.Collections.Generic;
using Jmodot.Core.Components;

namespace Jmodot.Implementation.Combat;

using Core.AI.BB;
using Core.Combat;

/// <summary>
/// A generic, reusable collision volume that detects interactions with HurtboxComponent3D nodes.
/// This component's sole responsibility is to act as a "sensor" for an attack. It becomes
/// active with a specific IAttackPayload and reports any valid hurtboxes it overlaps with.
/// It handles multi-hit prevention to ensure one attack swing doesn't hit the same target multiple times.
/// </summary>
[GlobalClass]
public partial class HitboxComponent3D : Area3D, IComponent
{
    #region Events

    /// <summary>
    /// Fired when this hitbox, while active, detects a valid HurtboxComponent3D.
    /// This is the primary event that the Hurtbox listens to in order to initiate the
    /// damage processing handshake.
    /// </summary>
    public event Action<HurtboxComponent3D, IAttackPayload> OnHurtboxDetected = delegate { };

    /// <summary>
    /// A cosmetic event fired when StartAttack() is called. Useful for triggering sounds or visuals.
    /// </summary>
    public event Action OnAttackStarted = delegate { };

    /// <summary>
    /// A cosmetic event fired when EndAttack() is called.
    /// </summary>
    public event Action OnAttackFinished = delegate { };

    #endregion

    #region Public Properties

    public bool IsActive { get; private set; }
    public IAttackPayload CurrentPayload { get; private set; }

    #endregion

    #region Private State

    private readonly HashSet<HurtboxComponent3D> _hitHurtboxes = new();

    #endregion

    #region IComponent Implementation

    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        // This component is self-contained and doesn't require blackboard dependencies.
        // We implement the interface for architectural consistency.
        IsInitialized = true;
        return true;
    }

    public void OnPostInitialize() { }

    public Node GetUnderlyingNode() => this;

    #endregion

    #region Godot Lifecycle

    public override void _Ready()
    {
        AreaEntered += OnAreaEntered;
        // Hitboxes should always start inactive.
        Monitoring = false;
        Monitorable = false;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Activates the hitbox with a specific attack payload, making it "hot".
    /// It clears the multi-hit tracking and immediately checks for any targets already inside it.
    /// </summary>
    public void StartAttack(IAttackPayload payload)
    {
        if (!IsInitialized) return;

        CurrentPayload = payload;
        IsActive = true;
        _hitHurtboxes.Clear();

        SetDeferred(PropertyName.Monitoring, true);
        SetDeferred(PropertyName.Monitorable, true);

        OnAttackStarted?.Invoke();

        // **ROBUSTNESS**: Immediately check for any hurtboxes already inside the area.
        // This handles lingering attacks (e.g., activating an aura) where OnAreaEntered would not fire.
        ProcessOverlappingAreas();
    }

    /// <summary>
    /// Deactivates the hitbox, making it "cold" and unable to detect hits.
    /// </summary>
    public void EndAttack()
    {
        if (!IsInitialized || !IsActive) return;

        CurrentPayload = null;
        IsActive = false;

        SetDeferred(PropertyName.Monitoring, false);
        SetDeferred(PropertyName.Monitorable, false);

        OnAttackFinished?.Invoke();
    }

    #endregion

    #region Core Logic

    private void OnAreaEntered(Area3D area)
    {
        if (area is HurtboxComponent3D hurtbox)
        {
            ProcessHit(hurtbox);
        }
    }

    private void ProcessOverlappingAreas()
    {
        foreach (var area in GetOverlappingAreas())
        {
            if (area is HurtboxComponent3D hurtbox)
            {
                ProcessHit(hurtbox);
            }
        }
    }

    /// <summary>
    /// The central validation logic for any potential hit.
    /// </summary>
    private void ProcessHit(HurtboxComponent3D hurtbox)
    {
        // 1. Check if the hitbox is active.
        if (!IsActive) return;

        // 2. Check if the hurtbox's owner is the same as the attacker to prevent self-damage.
        if (CurrentPayload?.Attacker != null && hurtbox.Owner == CurrentPayload.Attacker) return;

        // 3. CRITICAL: Check the HashSet to prevent multi-hitting. .Add() returns false if the item already exists.
        if (!_hitHurtboxes.Add(hurtbox)) return;

        // 4. If all checks pass, this is a valid, new hit. Broadcast the event.
        OnHurtboxDetected?.Invoke(hurtbox, CurrentPayload);
    }

    #endregion
}
