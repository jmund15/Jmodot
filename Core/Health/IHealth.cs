using System;

namespace Jmodot.Core.Health;

/// <summary>
/// Categorizes the cause of a health change so subscribers can filter feedback
/// (e.g., a sprite hit-flash plays for Direct hits but not for Tick damage —
/// which has its own per-tick visual that would otherwise stack with the flash).
/// </summary>
public enum DamageKind
{
    /// <summary>Primary impact — projectile contact, melee swing, explicit kill. Default.</summary>
    Direct = 0,

    /// <summary>Recurring damage from a status effect (burn, poison, bleed). Has its own per-tick visual.</summary>
    Tick = 1,

    /// <summary>Damage produced by a reaction or chained effect (spell-on-spell, water-on-fire).</summary>
    Reaction = 2,

    /// <summary>Hazard, fire-on-the-floor, lava, environmental DoT.</summary>
    Environmental = 3,
}

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

    /// <summary>
    /// Categorizes the cause so subscribers can filter (e.g., HitFlashComponent skips
    /// non-Direct so status ticks don't stack the generic white flash on top of their
    /// per-tick visual). Defaults to <see cref="DamageKind.Direct"/> for legacy callers.
    /// </summary>
    public DamageKind Kind { get; }

    public HealthChangeEventArgs(float newHealth, float previousHealth, float maxHealth, object source,
        DamageKind kind = DamageKind.Direct)
    {
        NewHealth = newHealth;
        PreviousHealth = previousHealth;
        MaxHealth = maxHealth;
        HealthDelta = newHealth - previousHealth;
        Source = source;
        Kind = kind;
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
    void TakeDamage(float amount, object source, DamageKind kind = DamageKind.Direct);
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
