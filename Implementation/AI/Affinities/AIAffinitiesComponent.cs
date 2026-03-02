namespace Jmodot.Implementation.AI.Affinities;

using System;
using System.Collections.Generic;
using BB;
using Core.AI.Affinities;
using Core.AI.BB;
using Core.Components;
using Core.Modifiers;
using Core.Shared;
using Modifiers;
using Modifiers.CalculationStrategies;
using Shared;
using GCol = Godot.Collections;

/// <summary>
///     The definitive "personality" component for an AI. It holds the runtime values for
///     all of the AI's core personality traits, using Affinity resources as type-safe keys.
///     Both high-level (Utility AI) and low-level (Steering) systems read from this single
///     source of truth to inform their decisions.
///
///     Internally backed by <see cref="ModifiableProperty{T}"/> with a clamped [0,1] calculation
///     strategy, giving affinities the full modifier pipeline (temporary buffs, status effects,
///     priority, tags) while preserving the simple Get/Set/Modify public API.
/// </summary>
[GlobalClass]
public partial class AIAffinitiesComponent : Node, IComponent, IBlackboardProvider
{
    /// <summary>
    /// Designer-authored base values. Converted to <see cref="ModifiableProperty{T}"/>
    /// instances during <see cref="Initialize"/> or lazily on first access.
    /// Values must be in [0, 1] range — out-of-range values are silently clamped at runtime.
    /// </summary>
    [Export] private GCol.Dictionary<Affinity, float> _baseValues = new();

    /// <summary>
    /// Runtime properties backed by the modifier pipeline.
    /// Lazily created on first access per affinity.
    /// </summary>
    private readonly Dictionary<Affinity, ModifiableProperty<float>> _properties = new();

    /// <summary>
    /// Shared clamped strategy instance — stateless, safe to share across all properties.
    /// </summary>
    private static readonly ClampedFloatCalculationStrategy AffinityStrategy = new()
    {
        MinValue = 0f,
        MaxValue = 1f
    };

    /// <summary>
    /// Fired when an affinity's effective value changes. Provides the affinity, old value, and new value.
    /// </summary>
    public event Action<Affinity, float, float> AffinityChanged = delegate { };

    #region IComponent

    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Converts all exported base values into <see cref="ModifiableProperty{T}"/> instances
    /// and wires change events.
    /// </summary>
    /// <remarks>
    /// The <paramref name="bb"/> parameter is unused — this component has no blackboard dependencies.
    /// It self-registers via <see cref="IBlackboardProvider.Provision"/> instead.
    /// </remarks>
    public bool Initialize(IBlackboard bb)
    {
        if (IsInitialized) { return true; }

        foreach (var (affinity, baseValue) in _baseValues)
        {
            GetOrCreateProperty(affinity, baseValue);
        }

        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }

    public event Action Initialized = delegate { };

    public Node GetUnderlyingNode() => this;

    #endregion

    #region IBlackboardProvider

    public (StringName Key, object Value)? Provision => (BBDataSig.Affinities, this);

    #endregion

    /// <summary>
    ///     Gets the current effective value of a specific affinity.
    ///     Returns the modifier-pipeline-resolved value, not the raw base.
    ///     Returns null if the affinity is not defined for this agent.
    /// </summary>
    public float? GetAffinity(Affinity affinity)
    {
        if (_properties.TryGetValue(affinity, out var prop))
        {
            return prop.Value;
        }

        // Fallback: check exported base values (pre-Initialize or never-set)
        if (_baseValues.TryGetValue(affinity, out float baseVal))
        {
            return baseVal;
        }

        return null;
    }

    /// <summary>
    /// Tries to get an affinity's effective value. Returns true if the affinity exists.
    /// </summary>
    public bool TryGetAffinity(Affinity affinity, out float value)
    {
        if (_properties.TryGetValue(affinity, out var prop))
        {
            value = prop.Value;
            return true;
        }

        if (_baseValues.TryGetValue(affinity, out float baseVal))
        {
            value = baseVal;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    ///     Sets the base value of an affinity at runtime. Modifiers are recalculated on top.
    ///     Fires AffinityChanged event if the effective value actually changes.
    /// </summary>
    public void SetAffinity(Affinity affinity, float value)
    {
        var prop = GetOrCreateProperty(affinity);
        float oldEffective = prop.Value;

        // Clamp at the base level too, for semantic safety
        prop.BaseValue = Mathf.Clamp(value, 0f, 1f);

        float newEffective = prop.Value;
        FireIfChanged(affinity, oldEffective, newEffective);
    }

    /// <summary>
    /// Modifies an affinity's base value by a delta. Useful for incremental changes.
    /// Creates the affinity with delta as value if it doesn't exist (treating missing as 0).
    /// </summary>
    public void ModifyAffinity(Affinity affinity, float delta)
    {
        var prop = GetOrCreateProperty(affinity);
        float currentBase = prop.BaseValue;
        SetAffinity(affinity, currentBase + delta);
    }

    /// <summary>
    /// Applies a temporary modifier to an affinity (e.g., a status effect raising Fear).
    /// Returns a handle for precise removal. The effective value is clamped to [0,1].
    /// </summary>
    public ModifierHandle AddModifier(Affinity affinity, IModifier<float> modifier, object owner)
    {
        var prop = GetOrCreateProperty(affinity);
        float oldEffective = prop.Value;

        Guid id = prop.AddModifier(modifier, owner);
        var handle = new ModifierHandle(prop, id);

        float newEffective = prop.Value;
        FireIfChanged(affinity, oldEffective, newEffective);

        return handle;
    }

    /// <summary>
    /// Removes a specific modifier by its handle.
    /// </summary>
    public void RemoveModifier(ModifierHandle? handle)
    {
        if (handle == null) { return; }

        // Find which affinity owns this property to fire the correct event
        Affinity? affinity = FindAffinityForProperty(handle.Property);
        if (affinity == null)
        {
            JmoLogger.Warning(this, $"RemoveModifier called with a handle whose property is not owned by this component. Modifier {handle.ModifierId} removed without event.");
        }

        var typedProp = (ModifiableProperty<float>)handle.Property;
        float oldEffective = affinity != null ? typedProp.Value : 0f;

        typedProp.RemoveModifier(handle.ModifierId);

        if (affinity != null)
        {
            float newEffective = typedProp.Value;
            FireIfChanged(affinity, oldEffective, newEffective);
        }
    }

    /// <summary>
    /// Removes all modifiers applied by a specific source across all affinities.
    /// </summary>
    public void RemoveAllModifiersFromSource(object owner)
    {
        foreach (var (affinity, prop) in _properties)
        {
            float oldEffective = prop.Value;
            prop.RemoveAllModifiersFromSource(owner);
            float newEffective = prop.Value;
            FireIfChanged(affinity, oldEffective, newEffective);
        }
    }

    /// <summary>
    /// Gets or lazily creates a <see cref="ModifiableProperty{T}"/> for the given affinity.
    /// </summary>
    /// <param name="baseValueOverride">
    /// If provided, overrides both the exported base value and the default (0).
    /// Used during Initialize() to seed from designer-authored values.
    /// Fallback order: explicit override → exported _baseValues entry → 0.
    /// </param>
    private ModifiableProperty<float> GetOrCreateProperty(Affinity affinity, float? baseValueOverride = null)
    {
        if (_properties.TryGetValue(affinity, out var existing))
        {
            return existing;
        }

        // Determine base value: explicit override > exported value > 0
        float baseValue = baseValueOverride
                          ?? (_baseValues.TryGetValue(affinity, out float exported) ? exported : 0f);

        var prop = new ModifiableProperty<float>(Mathf.Clamp(baseValue, 0f, 1f), AffinityStrategy);
        _properties[affinity] = prop;

        // Ensure the base values dict stays in sync for inspector reflection
        _baseValues[affinity] = Mathf.Clamp(baseValue, 0f, 1f);

        return prop;
    }

    /// <summary>
    /// Fires <see cref="AffinityChanged"/> if the effective value changed meaningfully.
    /// </summary>
    private void FireIfChanged(Affinity affinity, float oldEffective, float newEffective)
    {
        if (Mathf.Abs(oldEffective - newEffective) >= 0.001f)
        {
            AffinityChanged.Invoke(affinity, oldEffective, newEffective);
        }
    }

    /// <summary>
    /// Reverse-lookup: finds the Affinity key for a given property reference.
    /// </summary>
    private Affinity? FindAffinityForProperty(IModifiableProperty property)
    {
        foreach (var (affinity, prop) in _properties)
        {
            if (ReferenceEquals(prop, property))
            {
                return affinity;
            }
        }
        return null;
    }
}
