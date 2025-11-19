using System;

namespace Jmodot.Core.Health;

/// <summary>
/// A generic event argument class that provides detailed context about a health change.
/// </summary>
public class HealthChangeEventArgs : EventArgs
{
    public float NewHealth { get; }
    public float PreviousHealth { get; }
    public float MaxHealth { get; }
    public float HealthDelta { get; }
    /// <summary>
    /// The object, component, or system responsible for this health change.
    /// Can be cast to a more specific type (e.g., Player, Projectile, PoisonEffect)
    /// by the event listener to determine the cause.
    /// </summary>
    public object Source { get; }

    public HealthChangeEventArgs(float newHealth, float previousHealth, float maxHealth, object source)
    {
        NewHealth = newHealth;
        PreviousHealth = previousHealth;
        MaxHealth = maxHealth;
        HealthDelta = newHealth - previousHealth;
        Source = source;
    }
}

/// <summary>
/// Defines a read-only contract for components that have health.
/// Useful for UI, AI, and other observer systems.
/// </summary>
public interface IHealth
{
    event Action<HealthChangeEventArgs> OnHealthChanged;
    event Action<float> OnMaxHealthChanged;
    event Action<HealthChangeEventArgs> OnDied;
    event Action<HealthChangeEventArgs> OnResurrected;

    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsDead { get; }
}

/// <summary>
/// Defines a contract for components that can receive damage.
/// Used by systems that inflict damage, like hitboxes or environmental hazards.
/// </summary>
public interface IDamageable
{
    event Action<HealthChangeEventArgs> OnDamaged;
    /// <summary>
    /// Inflicts damage upon the component.
    /// </summary>
    /// <param name="amount">The amount of health to remove. Should be positive.</param>
    /// <param name="source">The object responsible for the damage.</param>
    void TakeDamage(float amount, object source);
}

/// <summary>
/// Defines a contract for components that can be healed.
/// Used by systems that restore health, like potions or healing spells.
/// </summary>
public interface IHealable
{
    event Action<HealthChangeEventArgs> OnHealed;
    /// <summary>
    /// Restores health to the component.
    /// </summary>
    /// <param name="amount">The amount of health to restore. Should be positive.</param>
    /// <param name="source">The object responsible for the healing.</param>
    void Heal(float amount, object source);
}
