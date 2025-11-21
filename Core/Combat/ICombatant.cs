namespace Jmodot.Core.Combat;

public interface ICombatant
{
    Node OwnerNode { get; }

    /// <summary>
    /// Service Locator: Allows effects to request specific systems (Health, Inventory)
    /// without coupling the interface to those types.
    /// </summary>
    bool TryGetSystem<T>(out T system) where T : class;

    /// <summary>
    /// The entry point for processing a validated hit.
    /// </summary>
    void ProcessPayload(IAttackPayload payload, HitContext context);
}
