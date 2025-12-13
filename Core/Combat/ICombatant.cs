namespace Jmodot.Core.Combat;

using System;
using AI.BB;
using Implementation.Combat.Status;
using Reactions;
using Shared;

public interface ICombatant : IGodotNodeInterface
{
    Node OwnerNode { get; }

    /// <summary>
    /// Service Locator: Allows effects to request specific systems (Health, Inventory)
    /// without coupling the interface to those types.
    /// </summary>
    IBlackboard Blackboard { get; }
    //bool TryGetSystem<T>(out T system) where T : class;
    // TODO: in the future, use a service locater dedicated to the combat system instead of a generic blackboard

    // The "Universal" Event for each combat effect's result
    event Action<CombatResult> CombatResultEvent;
    /// <summary>
    /// The entry point for processing a validated hit.
    /// </summary>
    void ProcessPayload(IAttackPayload payload, HitContext context);

    void ApplyEffect(ICombatEffect effect, HitContext context);
}
