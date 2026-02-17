namespace Jmodot.Implementation.Health;

// HealthComponent.cs
using Godot;
using System;
using AI.BB;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Stats;
using Jmodot.Core.Health;
using Jmodot.Core.Pooling;
using Jmodot.Core.Shared.Attributes;
using Shared;
using Attribute = Core.Stats.Attribute;

/// <summary>
/// Manages the health state of an entity, acting as a bridge between the IStatProvider
/// and health-related game logic. It functions as a robust state machine for an entity's
/// life cycle, orchestrating damage, healing, death, and resurrection.
///
/// This component does not own the MaxHealth stat; it reads it from the StatController,
/// treating it as the single source of truth for the entity's health potential. It enforces
/// strict rules for state transitions, such as preventing healing while dead and requiring
/// an explicit call to Resurrect() for revival.
///
/// Required BBDataSig Keys:
/// - "IStatProvider": An instance of a class implementing IStatProvider.
/// </summary>
[GlobalClass]
public partial class HealthComponent : Node, IComponent, IHealth, IDamageable, IHealable, IBlackboardProvider, IPoolResetable
{
    #region IBlackboardProvider Implementation
    /// <summary>
    /// Auto-registers this component with the blackboard during EntityNodeComponentsInitializer.
    /// </summary>
    public (StringName Key, object Value)? Provision => (BBDataSig.HealthComponent, this);
    #endregion

    #region Dependencies & Configuration

    [ExportGroup("Stat System Integration")]
    [Export, RequiredExport]
    private BaseFloatValueDefinition _maxHealthDefinition = null!;

    /// <summary>
    /// If true, current health will scale proportionally when max health changes.
    /// If false, it will remain the same unless it exceeds the new max.
    /// </summary>
    [ExportGroup("Configuration")]
    [Export]
    public bool ChangeHealthOnMaxChange { get; private set; } = true;

    /// <summary>
    /// If true, TakeDamage calls are ignored but other combat effects (knockback) still apply.
    /// Use this for "no damage but still physical" states like Collection phase.
    /// </summary>
    /// <remarks>
    /// Unlike HurtboxComponent3D.IsInvulnerable which blocks ALL hit processing,
    /// IsDamageImmune only blocks damage while allowing the combat effect flow to
    /// continue - meaning knockback and other effects still work normally.
    /// </remarks>
    [Export]
    public bool IsDamageImmune { get; set; } = false;

    /// <summary>
    /// If true, the entity can take damage but never dies (health caps at 1HP).
    /// OnDamaged still fires for visual feedback; OnDied never fires.
    /// </summary>
    [Export]
    public bool IsIndestructible { get; set; } = false;

    #endregion

    #region Events (IHealth, IDamageable, IHealable)

    /// <summary>
    /// Fired whenever CurrentHealth changes for any reason.
    /// </summary>
    public event Action<HealthChangeEventArgs> OnHealthChanged = delegate { };

    /// <summary>
    /// Fired when the final calculated value of the MaxHealth stat changes.
    /// </summary>
    public event Action<float> OnMaxHealthChanged = delegate { };

    /// <summary>
    /// Fired only when the entity transitions from an alive state to a dead state.
    /// </summary>
    public event Action<HealthChangeEventArgs> OnDied = delegate { };

    /// <summary>
    /// Fired only when the entity is explicitly resurrected from a dead state.
    /// </summary>
    public event Action<HealthChangeEventArgs> OnResurrected = delegate { };

    /// <summary>
    /// Fired whenever the entity's health decreases due to damage.
    /// </summary>
    public event Action<HealthChangeEventArgs> OnDamaged = delegate { };

    /// <summary>
    /// Fired whenever the entity's health increases due to healing.
    /// </summary>
    public event Action<HealthChangeEventArgs> OnHealed = delegate { };

    #endregion

    #region Public Properties

    /// <summary>
    /// The current health of the entity. Read-only from the outside.
    /// </summary>
    public float CurrentHealth => _currentHealth;

    /// <summary>
    /// The maximum health of the entity. This is a computed property that fetches
    /// the value directly from the IStatProvider, ensuring it's always up-to-date.
    /// </summary>
    public float MaxHealth => _maxHealthDefinition?.ResolveFloatValue(_statProvider) ?? 0f;

    /// <summary>
    /// The definitive life/death state of the entity.
    /// </summary>
    public bool IsDead { get; private set; }

    #endregion

    #region Private State

    private IStatProvider _statProvider = null!;
    private float _currentHealth;
    private float _lastResolvedMaxHealth;

    #endregion

    #region IComponent Implementation

    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        if (_maxHealthDefinition == null)
        {
            JmoLogger.Error(this, $"{nameof(_maxHealthDefinition)} must be assigned in the inspector for {nameof(HealthComponent)}.");
            return false;
        }

        // Stats are optional — ConstantFloatDefinition doesn't need them
        bb.TryGet(BBDataSig.Stats, out _statProvider);

        // Initialize health to its starting maximum value from the definition.
        _currentHealth = MaxHealth;
        _lastResolvedMaxHealth = _currentHealth;
        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize()
    {
        // Subscribe to changes in the MaxHealth stat after all components are initialized.
        // Only subscribe if stats are available (ConstantFloatDefinition doesn't use stats).
        if (_statProvider != null)
        {
            _statProvider.OnStatChanged += OnStatProviderStatChanged;
        }

        // Emit initial values for any listeners that are set up during their own Initialize phase.
        OnMaxHealthChanged?.Invoke(MaxHealth);
    }

    public event Action Initialized = delegate { };

    #endregion

    #region Godot Lifecycle

    public override void _ExitTree()
    {
        // Always unsubscribe from events when the node is removed from the scene
        // to prevent memory leaks and errors from the event being fired on a freed object.
        if (_statProvider != null)
        {
            _statProvider.OnStatChanged -= OnStatProviderStatChanged;
        }
    }

    #endregion

    #region IPoolResetable Implementation

    /// <summary>
    /// Resets health state for pool reuse. Clears all event subscribers to prevent
    /// handler accumulation across pool cycles. SpellBehavior.Initialize() subscribes
    /// Health.OnDied += _ => Destroy(...) every cycle — without clearing, the Nth reuse
    /// fires N death handlers, causing cascading Destroy calls.
    /// </summary>
    public void OnPoolReset()
    {
        // Clear all event subscribers to prevent accumulation
        OnHealthChanged = delegate { };
        OnMaxHealthChanged = delegate { };
        OnDied = delegate { };
        OnResurrected = delegate { };
        OnDamaged = delegate { };
        OnHealed = delegate { };

        // Unsubscribe from stat provider to prevent stale callbacks
        if (_statProvider != null)
        {
            _statProvider.OnStatChanged -= OnStatProviderStatChanged;
        }

        // Reset state
        _currentHealth = 0;
        _lastResolvedMaxHealth = 0;
        IsDead = false;
        IsDamageImmune = false;
        IsIndestructible = false;
        IsInitialized = false;
    }

    #endregion

    #region Public API (IDamageable & IHealable)

    /// <summary>
    /// Inflicts damage upon the component. Damage is ignored if the entity is already dead.
    /// </summary>
    /// <param name="amount">The positive amount of health to remove.</param>
    /// <param name="source">The object responsible for the damage (e.g., a projectile, player, or status effect).</param>
    public virtual void TakeDamage(float amount, object source)
    {
        if (amount <= 0 || IsDead || !IsInitialized)
        {
            return;
        }

        // Damage immunity check - knockback still applies via DamageResult.Force
        // because this blocks only the health modification, not the effect flow
        if (IsDamageImmune)
        {
            return;
        }

        float newHealth = _currentHealth - amount;

        // Indestructible: cap at 1HP minimum — takes damage but never dies
        if (IsIndestructible && newHealth <= 0)
        {
            newHealth = 1f;
        }

        SetHealth(newHealth, source);
    }

    /// <summary>
    /// Restores health to the component. Healing is ignored if the entity is dead.
    /// </summary>
    /// <param name="amount">The positive amount of health to restore.</param>
    /// <param name="source">The object responsible for the healing (e.g., a potion, spell, or healing zone).</param>
    public void Heal(float amount, object source)
    {
        // SAFEGUARD: Standard healing is ignored if the entity is dead.
        // This prevents accidental resurrection from area-of-effect heals.
        if (amount <= 0 || IsDead || !IsInitialized)
        {
            return;
        }

        SetHealth(_currentHealth + amount, source);
    }

    /// <summary>
    /// Explicitly resurrects the entity, setting its health to a specified value.
    /// This is the ONLY way to bring an entity back from the dead.
    /// </summary>
    /// <param name="healthOnRevive">The amount of health the entity will have after resurrection. Clamped between 1 and MaxHealth.</param>
    /// <param name="source">The object responsible for the resurrection (e.g., a resurrection spell or player ability).</param>
    public void Resurrect(float healthOnRevive, object source)
    {
        // Can only resurrect if currently dead.
        if (!IsDead || !IsInitialized)
        {
            JmoLogger.Warning(this, $"Can't resurrect an entity that isn't dead!");
            return;
        }

        float previousHealth = 0;
        IsDead = false; // Transition state before invoking events.

        _currentHealth = Mathf.Clamp(healthOnRevive, 1, MaxHealth);

        var eventArgs = new HealthChangeEventArgs(_currentHealth, previousHealth, MaxHealth, source);

        // Invoke the specific resurrection event first for systems that only care about this transition.
        OnResurrected?.Invoke(eventArgs);

        // Then invoke the general change/heal events for broader systems like UI.
        OnHealthChanged?.Invoke(eventArgs);
        OnHealed?.Invoke(eventArgs);
    }

    /// <summary>
    /// Forces the entity to die by setting its health to zero.
    /// </summary>
    /// <param name="source">The object responsible for the kill (e.g., a debug command, kill-zone).</param>
    public void Kill(object source)
    {
        if (IsDead || !IsInitialized)
        {
            JmoLogger.Warning(this, $"Can't kill an entity that's already dead!");
            return;
        }
        SetHealth(0, source);
    }

    /// <summary>
    /// Resets health to maximum. Works whether alive or dead.
    /// If dead, resurrects with full health. If alive, heals to max.
    /// </summary>
    /// <param name="source">The object responsible for the reset (e.g., game manager, reset system).</param>
    public void ResetToFull(object source)
    {
        if (!IsInitialized)
        {
            return;
        }

        if (IsDead)
        {
            Resurrect(MaxHealth, source);
        }
        else
        {
            float missing = MaxHealth - _currentHealth;
            if (missing > 0)
            {
                Heal(missing, source);
            }
        }
    }

    #endregion

    #region Core Logic & Event Handlers

    /// <summary>
    /// The central method for all health modifications. It calculates changes,
    /// clamps values, invokes all relevant events, and handles the death transition.
    /// </summary>
    private void SetHealth(float newHealth, object source)
    {
        float previousHealth = _currentHealth;
        float maxHealth = MaxHealth; // Cache for this scope to avoid repeated lookups.

        // Clamp the new health value within the valid range [0, MaxHealth].
        _currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);

        // If no actual change occurred after clamping, do nothing.
        if (Mathf.IsEqualApprox(_currentHealth, previousHealth))
        {
            return;
        }

        // --- Event Invocation ---
        var eventArgs = new HealthChangeEventArgs(_currentHealth, previousHealth, maxHealth, source);
        OnHealthChanged?.Invoke(eventArgs);

        if (eventArgs.HealthDelta < 0)
        {
            OnDamaged?.Invoke(eventArgs);
        }
        else
        {
            OnHealed?.Invoke(eventArgs);
        }

        // --- State Transition Logic ---
        // This method only handles the transition to death. Resurrection is handled explicitly.
        if (_currentHealth <= 0 && !IsDead)
        {
            IsDead = true;
            OnDied?.Invoke(eventArgs);
        }
    }

    /// <summary>
    /// Handles stat changes by re-resolving MaxHealth through the definition.
    /// Works with any BaseFloatValueDefinition subclass — the definition decides
    /// whether the changed attribute is relevant.
    /// </summary>
    private void OnStatProviderStatChanged(Attribute attribute, Variant newValue)
    {
        float newMaxHealth = MaxHealth;
        if (Mathf.IsEqualApprox(newMaxHealth, _lastResolvedMaxHealth)) { return; }

        float previousMaxHealth = _lastResolvedMaxHealth;
        _lastResolvedMaxHealth = newMaxHealth;

        OnMaxHealthChanged?.Invoke(newMaxHealth);

        if (ChangeHealthOnMaxChange)
        {
            // Calculate the current health percentage against the old max health.
            float healthPercentage = previousMaxHealth > 0 ? _currentHealth / previousMaxHealth : 1f;
            // Apply the same percentage to the new max health to scale current health.
            SetHealth(newMaxHealth * healthPercentage, this);
        }
        else
        {
            // If not scaling, simply ensure current health doesn't exceed the new max.
            SetHealth(_currentHealth, this);
        }
    }

    #endregion

    public Node GetUnderlyingNode()
    {
        return this;
    }
}
