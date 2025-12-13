using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.Health;
using Jmodot.Core.Health;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat;

using Core.Combat.Reactions;
using Status;

/// <summary>
/// The central brain for combat interactions on an entity.
/// Receives payloads from the Hurtbox and delegates logic to Effects.
/// Acts as a Service Locator for combat-related components.
/// </summary>
[GlobalClass]
public partial class CombatantComponent : Node, IComponent, ICombatant
{
    #region Dependencies
    [Export] public HealthComponent Health { get; private set; } = null!;
    [Export] public StatusEffectComponent StatusComponent { get; private set; } = null!;
    #endregion

    // The "Universal" Event.
    // Listeners use pattern matching: if (result is DamageResult dr) ...
    public event Action<CombatResult> CombatResultEvent = delegate { };

    #region Node Overrides
    public override void _Ready()
    {

    }
    #endregion

    #region ICombatant Implementation
    public Node OwnerNode => GetOwner();
    public IBlackboard Blackboard { get; private set; } = null!;

    public void ProcessPayload(IAttackPayload payload, HitContext context)
    {
        if (payload.Effects == null || payload.Effects.Count == 0)
        {
            return;
        }
        foreach (var effect in payload.Effects)
        {
            ApplyEffect(effect, context);
        }
    }

    /// <summary>
    /// Breaking this out into a single function to allow to single effect applications (from status runners or elsewhere)
    /// </summary>
    /// <param name="effect"></param>
    /// <param name="context"></param>
    public void ApplyEffect(ICombatEffect effect, HitContext context)
    {
        if (effect == null)
        {
            return;
        }

        // 1. EXECUTE
        // The Effect contains the logic (Calculation, Armor Check, Health Modification).
        // The Combatant just provides the context (itself).
        CombatResult? result = effect.Apply(this, context);

        // 2. REPORT
        // If something actually happened, broadcast it.
        if (result != null)
        {
            CombatResultEvent?.Invoke(result);
        }
    }

    #endregion

    /// <summary>
    /// Called by StatusEffectComponent when a runner finishes.
    /// Bridges the gap between the Status system and the generic Reaction system.
    /// </summary>
    private void StatusRemoved(StatusRunner runner, bool wasDispelled)
    {
        var result = new StatusExpiredResult
        {
            Source = null, // Expiration is internal
            Target = OwnerNode,
            Tags = runner.Tags,
            WasDispelled = wasDispelled
        };

        CombatResultEvent?.Invoke(result);
    }

    #region IComponent Implementation
    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        Blackboard = bb;

        // If dependencies aren't assigned in Inspector, try to find them
        if (Health == null)
        {
            Health = bb.Get<HealthComponent>(BBDataSig.HealthComponent);
        }

        if (StatusComponent == null)
        {
             StatusComponent = bb.Get<StatusEffectComponent>(BBDataSig.StatusEffects);
        }

        IsInitialized = true;
        Initialized?.Invoke();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize()
    {
        StatusComponent.StatusRemoved += StatusRemoved;
    }
    public event Action? Initialized;

    public Node GetUnderlyingNode() => this;
    #endregion
}
