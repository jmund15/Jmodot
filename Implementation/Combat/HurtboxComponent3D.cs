using Godot;
using System;
using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Identification;

namespace Jmodot.Implementation.Combat;

using Core.Movement;
using Implementation.AI.BB;
using Implementation.Shared;

/// <summary>
/// Represents the damageable zone of an entity.
/// Acts as a Gateway/Filter to the ICombatant found in the Blackboard.
/// </summary>
[GlobalClass]
public partial class HurtboxComponent3D : Area3D, IComponent, IBlackboardProvider
{
    #region IBlackboardProvider Implementation
    /// <summary>
    /// Auto-registers this component with the blackboard during EntityNodeComponentsInitializer.
    /// </summary>
    public (StringName Key, object Value)? Provision => (BBDataSig.HurtboxComponent, this);
    #endregion

    #region Events
    /// <summary>
    /// Fired when a hit is successfully accepted and forwarded to the Combatant.
    /// </summary>
    public event Action<IAttackPayload, HitContext> OnHitReceived = delegate { };
    #endregion

    #region Configuration

    /// <summary>
    /// If true, this entity ignores all hits cheaply (Gatekeeper).
    /// </summary>
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
        // Retrieve the Combatant dependency directly from the Blackboard.
        // This ensures decoupling; the Hurtbox doesn't care *how* the Combatant is implemented.
        if (!bb.TryGet(BBDataSig.CombatantComponent, out _combatant))
        {
            // Fail initialization if the Brain (Combatant) is missing.
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
    /// Checks whether this hurtbox can currently accept a hit.
    /// Extracted from <see cref="ProcessHit"/> so callers (notably <see cref="HitboxComponent3D"/>'s
    /// payload-interception path) can pre-validate before invoking expensive interceptor logic.
    /// </summary>
    /// <returns>True if the hurtbox is active, initialized, has a combatant, and is not invulnerable.</returns>
    public bool CanReceiveHit()
    {
        if (!IsActive) { return false; }
        if (!IsInitialized || _combatant == null) { return false; }
        if (IsInvulnerable) { return false; }
        return true;
    }

    /// <summary>
    /// Called DIRECTLY by HitboxComponent3D.
    /// This is the receiving end of the Handshake.
    /// </summary>
    /// <returns>True if the hit was processed, False if rejected.</returns>
    public bool ProcessHit(IAttackPayload payload)
    {
        if (!CanReceiveHit()) { return false; }

        // 3. Context Creation
        // Maps the Payload Source to the Context so effects know what hit them.
        Vector3 epicenter = GetEpicenterPosition(payload.Source);
        HitContext context = new HitContext
        {
            Attacker = payload.Attacker,
            Source = payload.Source,
            HitDirection = CalculateHitDirection(payload.Source),
            ImpactVelocity = CalculateImpactVelocity(payload.Source),
            EpicenterPosition = epicenter,
            DistanceFromEpicenter = GlobalPosition.DistanceTo(epicenter)
        };

        // 3.5. Reaction-resolver consultation (A2)
        // If the project wired CombatFactoryDefaults.ReactionResolver, query for matching
        // reactions (e.g., shatter on frozen, oil+fire→explosion). Outcomes apply via
        // the resolver's project-side machinery; the resolver returns a (possibly
        // damage-stripped) payload to forward when an Exclusive reaction matched.
        // No resolver wired → null returned → fall through to forward original payload.
        var payloadToForward = ConsultReactionResolver(payload, context);

        // 4. Forward to Brain
        // The Combatant executes the logic defined in the (possibly filtered) effects.
        _combatant.ProcessPayload(payloadToForward, context);

        // 5. Feedback
        // Notify with the ORIGINAL payload — interceptor/resolver-side filtering must not
        // affect downstream observers (e.g., post-hit VFX hooks reading base damage).
        OnHitReceived?.Invoke(payload, context);
        return true;
    }

    /// <summary>
    /// Consult <see cref="CombatFactoryDefaults.ReactionResolver"/> for matching reactions
    /// and apply outcomes. Returns the payload to forward to <see cref="ICombatant.ProcessPayload"/>
    /// — equal to <paramref name="payload"/> when no resolver is wired or no reactions matched;
    /// may be a damage-stripped wrapper when an Exclusive reaction matched.
    /// </summary>
    private IAttackPayload ConsultReactionResolver(IAttackPayload payload, HitContext context)
    {
        var resolver = CombatFactoryDefaults.ReactionResolver;
        if (resolver == null) { return payload; }

        // Resolve attacker Identity by walking up from payload.Attacker.
        var attackerIdentity = ResolveIdentity(payload.Attacker);
        if (attackerIdentity == null) { return payload; }

        // Resolve defender Identity by walking up from the combatant's owner.
        var defenderNode = _combatant.OwnerNode;
        var defenderIdentity = ResolveIdentity(defenderNode);
        if (defenderIdentity == null) { return payload; }

        // Pull active status tags from the defender's StatusEffectComponent (BB-registered).
        var activeTags = ResolveDefenderActiveTags();

        return resolver.ConsultAndApply(
            attackerIdentity,
            defenderIdentity,
            activeTags,
            payload.Attacker,
            defenderNode,
            payload,
            context);
    }

    /// <summary>Walk up the tree from <paramref name="node"/> looking for the first ancestor
    /// implementing <see cref="IIdentifiable"/>. Returns null if none found.</summary>
    private static Identity? ResolveIdentity(Node? node)
    {
        var current = node;
        while (current != null)
        {
            if (current is IIdentifiable identifiable)
            {
                return identifiable.GetIdentity();
            }
            current = current.GetParent();
        }
        return null;
    }

    /// <summary>Aggregate the defender's currently-active <see cref="CombatTag"/>s from its
    /// <see cref="StatusEffectComponent"/> (looked up via the combatant's blackboard). Returns
    /// an empty list when no status component is registered (defenders without status state).</summary>
    private IReadOnlyList<CombatTag> ResolveDefenderActiveTags()
    {
        if (_combatant?.Blackboard == null) { return System.Array.Empty<CombatTag>(); }
        if (!_combatant.Blackboard.TryGet<StatusEffectComponent>(BBDataSig.StatusEffects, out var statusComp) || statusComp == null)
        {
            return System.Array.Empty<CombatTag>();
        }
        return statusComp.GetActiveTags();
    }
    #endregion
    #region Core Logic

    private void Activate()
    {
        if (IsActive) { return; }
        // Passive detection. We don't scan for others (Monitoring=False),
        // but we allow others to scan us (Monitorable=True).

        // SetDeferred ensures we don't crash if called during a physics callback.
        SetDeferred(PropertyName.Monitoring, false);
        SetDeferred(PropertyName.Monitorable, true);

        SetPhysicsProcess(true);
        IsActive = true;
    }

    private void Deactivate()
    {
        if (!IsActive) { return; }
        IsActive = false;
        // SetDeferred ensures we don't crash if called during a physics callback.
        SetDeferred(PropertyName.Monitoring, false);
        SetDeferred(PropertyName.Monitorable, false);
        SetPhysicsProcess(false);
    }
    private Vector3 CalculateHitDirection(Node source)
    {
        // 1. VELOCITY BASED (for projectiles - direction they're traveling)
        if (source is IVelocityProvider3D velocityProvider && velocityProvider.LinearVelocity.LengthSquared() > 0.01f)
        {
            return velocityProvider.LinearVelocity.Normalized();
        }

        if (source is CharacterBody3D cb && cb.Velocity.LengthSquared() > 0.01f)
        {
            return cb.Velocity.Normalized();
        }

        if (source is RigidBody3D rb && rb.LinearVelocity.LengthSquared() > 0.01f)
        {
            return rb.LinearVelocity.Normalized();
        }

        if (source is StaticBody3D sb && sb.ConstantLinearVelocity.LengthSquared() > 0.01f)
        {
            return sb.ConstantLinearVelocity.Normalized();
        }

        // 2. POSITION BASED (for AOE/explosions - push away from epicenter)
        if (source is Node3D source3D)
        {
            Vector3 direction = GlobalPosition - source3D.GlobalPosition;
            if (direction.LengthSquared() > 0.01f)
            {
                return direction.Normalized();
            }
        }

        // 3. FALLBACK: Random XZ direction (e.g., standing directly on explosion)
        return JmoRng.GetRndVector3ZeroY();
    }

    private Vector3 CalculateImpactVelocity(Node source)
    {
        // 1. Check for Interface (Preferred)
        if (source is Core.Movement.IVelocityProvider3D velocityProvider)
        {
            return velocityProvider.LinearVelocity;
        }

        // 2. Legacy Fallback (Casting)
        if (source is CharacterBody3D cb) { return cb.Velocity; }
        if (source is RigidBody3D rb) { return rb.LinearVelocity; }
        if (source is StaticBody3D sb) { return sb.ConstantLinearVelocity; }

        // 3. Default
        return Vector3.Zero;
    }

    private Vector3 GetEpicenterPosition(Node source)
    {
        // Use the source's GlobalPosition as the epicenter.
        // For hitboxes, this is typically the center of the collision shape.
        if (source is Node3D source3D)
        {
            return source3D.GlobalPosition;
        }

        // Fallback to self position if source isn't 3D
        return GlobalPosition;
    }
    #endregion
}
