#region

using System;
using System.Collections.Generic;
using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.CalculationStrategy;
using Jmodot.Core.Stats.Mechanics;
using Jmodot.Implementation.Modifiers.CalculationStrategies;

#endregion

// Alias to prevent conflict with Godot.System

namespace Jmodot.Core.Stats;

/// <summary>
///     The definitive runtime "character sheet" and single source of truth for all of an entity's
///     dynamic properties. This class manages both universal stats (like Max Health) and contextual
///     stats (like Ground Speed), applying modifiers and calculating the final values on demand.
///     It is initialized from a CharacterArchetype and serves as the central hub for any system
///     needing to query or modify character data.
/// </summary>
[GlobalClass]
// TODO: MAKE THIS A INTERFACE for better dependency injection and scalability
public partial class StatController : Node, IStatProvider
{
    // A dedicated library for mechanics. The value is a ModifiableProperty
    // so that buffs/debuffs can change a mechanic's data at runtime (e.g., a "Super Jump" power-up).
    private readonly Dictionary<MechanicType, ModifiableProperty<MechanicData>> _mechanics = new();

    /// <summary>
    ///     Stores stats that are specific to a particular MovementMode. These provide the context-specific
    ///     overrides and properties needed for different states of motion.
    ///     Example: Ground Max Speed, Air Acceleration, Swim Friction.
    /// </summary>
    private readonly Dictionary<MovementMode, Dictionary<Attribute, ModifiableProperty<Variant>>> _movementModeStats =
        new();
    // --- Private State ---

    /// <summary>
    ///     Stores all universal stats that apply to the entity regardless of its state.
    ///     Example: Max Health, Strength, Intelligence.
    /// </summary>
    private readonly Dictionary<Attribute, ModifiableProperty<Variant>> _universalStats = new();

    // --- Public Events ---

    /// <summary>
    ///     Emitted whenever the final calculated value of a stat changes.
    ///     This is the primary mechanism for decoupled systems (like UI or HealthComponents) to react to data changes.
    ///     Note: This is currently emitted only during initialization. A full implementation would require
    ///     ModifiableProperty to raise an event when its value changes, which this StatController would subscribe to.
    /// </summary>
    /// <param name="attribute">The attribute whose value has changed.</param>
    /// <param name="newValue">The new final calculated value.</param>
    public event Action<Attribute, Variant> OnStatChanged = null!;

    // --- Public API ---

    /// <summary>
    ///     Retrieves the underlying ModifiableProperty object for a given attribute. This is the
    ///     correct method to use for systems that need to ADD or REMOVE modifiers (e.g., buff/debuff systems).
    ///     It automatically handles the contextual fallback logic.
    /// </summary>
    /// <param name="attribute">The attribute to retrieve the property for.</param>
    /// <param name="context">Optional: The current MovementMode. If provided, will search for a contextual stat first.</param>
    /// <returns>The ModifiableProperty object, or null if the entity does not have the specified attribute.</returns>
    public ModifiableProperty<Variant>? GetStat(Attribute attribute, MovementMode? context = null)
    {
        // First, attempt to find the most specific, contextual version of the stat.
        if (context != null &&
            _movementModeStats.TryGetValue(context, out var modeStats) &&
            modeStats.TryGetValue(attribute, out var contextualProp))
            return contextualProp;

        // If no contextual version exists, fall back to the universal version.
        if (_universalStats.TryGetValue(attribute, out var universalProp)) return universalProp;

        // The entity does not possess this stat in any context.
        return null;
    }

    /// <summary>
    ///     The primary, type-safe "safe gate" for retrieving the final, calculated VALUE of a stat.
    ///     This is the correct method for any system that simply needs to consume data (e.g., movement logic, UI).
    ///     It performs runtime type checking to prevent crashes and handles the contextual fallback logic.
    /// </summary>
    /// <typeparam name="T">The expected type of the stat's value (e.g., float, bool, Vector3).</typeparam>
    /// <param name="attribute">The attribute whose value is being requested.</param>
    /// <param name="context">Optional: The current MovementMode for contextual lookups.</param>
    /// <param name="defaultValue">The value to return if the stat doesn't exist or if a type mismatch occurs.</param>
    /// <returns>The final calculated value of the stat, or the default value on failure.</returns>
    public T GetStatValue<[MustBeVariant] T>(Attribute attribute, MovementMode? context = null,
        T defaultValue = default)
    {
        var prop = GetStat(attribute, context);
        if (prop != null)
        {
            // Runtime Type Check: Ensure the requested type matches the stored Variant's type.
            if (prop.Value.Obj is T) //.Value.VariantType == Variant.Type.From<T>())
            {
                return prop.Value.As<T>();
            }

            // This indicates a logic error somewhere in the code or a design error in the data.
            // Log a detailed error to help developers debug it quickly.
            GD.PrintErr($"Stat Type Mismatch: Failed to get value for attribute '{attribute?.ResourceName}'. ",
                $"Requested type '{typeof(T).Name}' but the stat's actual type is '{prop.Value.VariantType}'. ",
                "Returning default value.");
            return defaultValue;
        }

        // The stat was not found in any context.
        return defaultValue;
    }

    public MechanicData? GetMechanicData(MechanicType mechanicType)
    {
        if (mechanicType != null && _mechanics.TryGetValue(mechanicType, out var modifiableData))
            // Return the final, potentially modified value.
            return modifiableData.Value;

        GD.PrintErr($"Entity does not have mechanic data for '{mechanicType?.MechanicName ?? "NULL"}'");
        return null;
    }

    // --- Initialization ---

    /// <summary>
    ///     Configures and populates the entire StatController based on the data defined in a
    ///     CharacterArchetype resource. This method builds the runtime ModifiableProperty objects,
    ///     assigns the correct calculation strategies, and sets their base values.
    /// </summary>
    /// <param name="archetype">The data template used to define this entity's stats.</param>
    public void InitializeFromArchetype(CharacterArchetype archetype)
    {
        // --- 1. Initialize Universal Stats ---
        var defaultOverrideStrategy = new VariantDefaultCalculationStrategy();

        foreach (var entry in archetype.UniversalAttributes)
        {
            var attribute = entry.Key;
            var baseValue = entry.Value;

            // Look up the specific strategy assigned in the archetype for this attribute.
            archetype.UniversalAttributeStrategies.TryGetValue(attribute, out var specificStrategy);

            // Use the specific strategy if provided; otherwise, fall back to the safe default (override).
            var strategyToUse = specificStrategy ?? defaultOverrideStrategy;

            _universalStats[attribute] = new ModifiableProperty<Variant>(baseValue, strategyToUse);
        }

        // --- 2. Initialize Contextual Movement Stats ---
        // For convenience and the 99% use case, we assume movement stats are floats and use the
        // standard float calculation pipeline as the default. This can be overridden in the VelocityProfile.
        var defaultMovementStrategy = new FloatCalculationStrategy();

        foreach (var modeEntry in archetype.MovementProfiles)
        {
            var mode = modeEntry.Key;
            var profile = modeEntry.Value;
            _movementModeStats[mode] = new Dictionary<Attribute, ModifiableProperty<Variant>>();

            foreach (var attrEntry in profile.Attributes)
            {
                var attribute = attrEntry.Key;
                var baseValue = attrEntry.Value;

                // Check if the VelocityProfile provides a specific strategy override for this attribute.
                profile.AttributeStrategies.TryGetValue(attribute, out var specificStrategy);

                // Use the override if it exists; otherwise, use the default for movement stats.
                var strategyToUse = specificStrategy ?? defaultMovementStrategy;

                _movementModeStats[mode][attribute] = new ModifiableProperty<Variant>(baseValue, strategyToUse);
            }
        }

        // --- 3. Post-Initialization Notification ---
        // Emit an initial change event for all stats so listening systems can sync their initial state.
        foreach (var stat in _universalStats) OnStatChanged?.Invoke(stat.Key, stat.Value.Value);
    }

    public ModifiableProperty<MechanicData>? GetMechanic(MechanicType mechanicType)
    {
        if (mechanicType != null && _mechanics.TryGetValue(mechanicType, out var modifiableData)) return modifiableData;
        return null;
    }
}