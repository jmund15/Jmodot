using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;

namespace Jmodot.Implementation.Combat;

using Core.Movement;
using Implementation.AI.BB;
using Implementation.Shared;

/// <summary>
/// 2D twin of <see cref="HurtboxComponent3D"/>. Represents the damageable zone
/// of a 2D entity. Acts as a Gateway/Filter to the ICombatant found in the Blackboard.
/// Registers on the shared BBDataSig.HurtboxComponent key — consumers dispatch by
/// entity dimension.
/// </summary>
[GlobalClass]
public partial class HurtboxComponent2D : Area2D, IComponent, IBlackboardProvider
{
    #region IBlackboardProvider Implementation
    public (StringName Key, object Value)? Provision => (BBDataSig.HurtboxComponent, this);
    #endregion

    #region Events
    /// <summary>
    /// Fired when a hit is successfully accepted and forwarded to the Combatant.
    /// </summary>
    public event Action<IAttackPayload, HitContext2D> OnHitReceived = delegate { };
    #endregion

    #region Configuration
    [ExportGroup("State")]
    [Export] public bool IsInvulnerable { get; set; } = false;
    #endregion

    #region State
    public bool IsActive { get; private set; }
    private ICombatant _combatant;
    #endregion

    #region IComponent Implementation
    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        if (!bb.TryGet(BBDataSig.CombatantComponent, out _combatant))
        {
            return false;
        }

        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize()
    {
        Activate();
    }
    public event Action Initialized = delegate { };

    public Node GetUnderlyingNode() => this;
    #endregion

    #region Godot Lifecycle
    public override void _Ready()
    {
    }
    #endregion

    #region Public API

    /// <summary>
    /// Called DIRECTLY by HitboxComponent2D. Receiving end of the handshake.
    /// </summary>
    public bool ProcessHit(IAttackPayload payload)
    {
        if (!IsActive) { return false; }
        if (!IsInitialized || _combatant == null) { return false; }
        if (IsInvulnerable) { return false; }

        Vector2 epicenter = GetEpicenterPosition(payload.Source);
        HitContext2D context = new HitContext2D
        {
            Attacker = payload.Attacker,
            Source = payload.Source,
            HitDirection = CalculateHitDirection(payload.Source),
            ImpactVelocity = CalculateImpactVelocity(payload.Source),
            EpicenterPosition = epicenter,
            DistanceFromEpicenter = GlobalPosition.DistanceTo(epicenter)
        };

        // Forward to Brain. The Combatant's ProcessPayload signature is dimension-
        // agnostic (consumes HitContext by reference); 2D callers pass HitContext2D
        // as the object — the effect pipeline can read it back by type if needed.
        // For strict dimension dispatch, ICombatant should eventually accept either
        // context type. For now we adapt by passing the 2D context through the
        // shared Node fields (Attacker, Source); direction/velocity are 2D-specific
        // and read from HitContext2D in 2D-aware effects.
        _combatant.ProcessPayload(payload, ToHitContextAdapter(context));

        OnHitReceived?.Invoke(payload, context);
        return true;
    }
    #endregion

    #region Core Logic

    private void Activate()
    {
        if (IsActive) { return; }
        SetDeferred(PropertyName.Monitoring, false);
        SetDeferred(PropertyName.Monitorable, true);
        SetPhysicsProcess(true);
        IsActive = true;
    }

    private void Deactivate()
    {
        if (!IsActive) { return; }
        IsActive = false;
        SetDeferred(PropertyName.Monitoring, false);
        SetDeferred(PropertyName.Monitorable, false);
        SetPhysicsProcess(false);
    }

    private Vector2 CalculateHitDirection(Node source)
    {
        // 1. VELOCITY BASED (projectiles — direction they're traveling)
        if (source is IVelocityProvider2D velocityProvider && velocityProvider.LinearVelocity.LengthSquared() > 0.01f)
        {
            return velocityProvider.LinearVelocity.Normalized();
        }

        if (source is CharacterBody2D cb && cb.Velocity.LengthSquared() > 0.01f)
        {
            return cb.Velocity.Normalized();
        }

        if (source is RigidBody2D rb && rb.LinearVelocity.LengthSquared() > 0.01f)
        {
            return rb.LinearVelocity.Normalized();
        }

        if (source is StaticBody2D sb && sb.ConstantLinearVelocity.LengthSquared() > 0.01f)
        {
            return sb.ConstantLinearVelocity.Normalized();
        }

        // 2. POSITION BASED (AOE/explosions — push away from epicenter)
        if (source is Node2D source2D)
        {
            Vector2 direction = GlobalPosition - source2D.GlobalPosition;
            if (direction.LengthSquared() > 0.01f)
            {
                return direction.Normalized();
            }
        }

        // 3. FALLBACK: Random direction (e.g., standing directly on explosion)
        return JmoRng.GetRndVector2();
    }

    private Vector2 CalculateImpactVelocity(Node source)
    {
        if (source is IVelocityProvider2D velocityProvider)
        {
            return velocityProvider.LinearVelocity;
        }

        if (source is CharacterBody2D cb) { return cb.Velocity; }
        if (source is RigidBody2D rb) { return rb.LinearVelocity; }
        if (source is StaticBody2D sb) { return sb.ConstantLinearVelocity; }

        return Vector2.Zero;
    }

    private Vector2 GetEpicenterPosition(Node source)
    {
        if (source is Node2D source2D)
        {
            return source2D.GlobalPosition;
        }
        return GlobalPosition;
    }

    /// <summary>
    /// Adapts a HitContext2D into a HitContext so the existing dimension-agnostic
    /// ICombatant.ProcessPayload surface can consume it. 2D-specific fields
    /// (HitDirection, ImpactVelocity, EpicenterPosition) are zero-padded on the
    /// 3D axes. 2D-aware effects should read the richer HitContext2D via the
    /// OnHitReceived event instead.
    /// </summary>
    private static HitContext ToHitContextAdapter(HitContext2D c) => new HitContext
    {
        Attacker = c.Attacker,
        Source = c.Source,
        HitDirection = new Vector3(c.HitDirection.X, 0f, c.HitDirection.Y),
        ImpactVelocity = new Vector3(c.ImpactVelocity.X, 0f, c.ImpactVelocity.Y),
        EpicenterPosition = new Vector3(c.EpicenterPosition.X, 0f, c.EpicenterPosition.Y),
        DistanceFromEpicenter = c.DistanceFromEpicenter
    };

    #endregion
}
