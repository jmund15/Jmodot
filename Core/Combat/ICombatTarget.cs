namespace Jmodot.Core.Combat;

/// <summary>
/// An interface for any component that can be a target of a combat payload.
/// Its sole responsibility is to receive a payload and route the effects within it.
/// </summary>
public interface ICombatTarget
{
    /// <summary>
    /// Receives an attack payload and processes its constituent effects.
    /// </summary>
    void ProcessPayload(IAttackPayload payload);
}
