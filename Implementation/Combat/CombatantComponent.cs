using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.Health;
using Jmodot.Core.Health;
using Jmodot.Implementation.AI.BB;

namespace Jmodot.Implementation.Combat;

/// <summary>
/// The central brain for combat interactions on an entity.
/// Receives payloads from the Hurtbox and delegates logic to Effects.
/// Acts as a Service Locator for combat-related components.
/// </summary>
[GlobalClass]
public partial class CombatantComponent : Node, IComponent, ICombatant
{
    #region Dependencies
    [Export]
    public HealthComponent Health { get; private set; }

    [Export]
    public StatusEffectComponent StatusComponent { get; private set; }
    #endregion

    #region ICombatant Implementation
    public Node OwnerNode => GetOwner();
    public IBlackboard Blackboard { get; private set; }

    public void ProcessPayload(IAttackPayload payload, HitContext context)
    {
        if (payload.Effects == null || payload.Effects.Count == 0) return;

        foreach (var effect in payload.Effects)
        {
            effect.Apply(this, context);
        }
    }
    #endregion

    #region IComponent Implementation
    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        Blackboard = bb;

        // If dependencies aren't assigned in Inspector, try to find them
        if (Health == null)
        {
            Health = GetParent().GetNodeOrNull<HealthComponent>("HealthComponent");
        }

        if (StatusComponent == null)
        {
             StatusComponent = GetParent().GetNodeOrNull<StatusEffectComponent>("StatusEffectComponent");
        }

        // Register components to Blackboard for Effects to find
        if (Health != null)
        {
            Blackboard.Set(BBDataSig.HealthComp, Health);
        }
        else
        {
             GD.PrintErr($"{nameof(CombatantComponent)} on {Name}: HealthComponent is missing!");
        }

        if (StatusComponent != null)
        {
            Blackboard.Set(BBDataSig.StatusEffects, StatusComponent);
        }
        else
        {
             GD.PrintErr($"{nameof(CombatantComponent)} on {Name}: StatusEffectComponent is missing!");
        }

        IsInitialized = true;
        return true;
    }

    public void OnPostInitialize() { }

    public Node GetUnderlyingNode() => this;
    #endregion
}
