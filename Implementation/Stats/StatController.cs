namespace Jmodot.Core.Stats;

using System;
using System.Collections.Generic;
using Implementation.Modifiers.CalculationStrategies;
using Implementation.Shared;
using Mechanics;
using Microsoft.CSharp.RuntimeBinder;
using Modifiers;
using Modifiers.CalculationStrategies;

/// <summary>
///     The definitive runtime "character sheet" and single source of truth for all of an entity's
///     dynamic properties. This class manages both universal stats (like Max Health) and contextual
///     stats (like Ground Speed), applying modifiers and calculating the final values on demand.
///     It is initialized from a CharacterArchetype and serves as the central hub for any system
///     needing to query or modify character data.
/// </summary>
[GlobalClass]
public partial class StatController : Node, IStatProvider
{
    /// <summary>
    ///     Stores stats that are specific to a particular MovementMode. These provide the context-specific
    ///     overrides and properties needed for different states of motion.
    ///     Example: Ground Max Speed, Air Acceleration, Swim Friction.
    /// </summary>
    private readonly Dictionary<StatContext, Dictionary<Attribute, IModifiableProperty>> _contextualStats =
        new();
    /// <summary>
    ///     Stores all universal stats that apply to the entity regardless of its state.
    ///     Example: Max Health, Strength, Intelligence.
    /// </summary>
    private readonly Dictionary<Attribute, IModifiableProperty> _universalStats = new();
    // A dedicated library for mechanics.
    // Buffs/debuffs can change a mechanic's data at runtime (e.g., a "Jump Power" Attribute).
    private readonly Dictionary<MechanicType, MechanicData> _mechanicLibrary = new();

    // Default strategies (can make static if we make the get calc strat static)
    private readonly FloatCalculationStrategy _defaultFloatCalcStrat = new();
    private readonly BoolOverrideStrategy _defaultBoolCalcStrat = new();

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
        foreach (var entry in archetype.UniversalAttributes)
        {
            var attribute = entry.Key;
            var baseValue = entry.Value;

            // Look up the specific strategy assigned in the archetype for this attribute.
            archetype.UniversalAttributeStrategies.TryGetValue(attribute, out var specificStrategy);

            _universalStats[attribute] = GetAttributeStrategy(attribute, baseValue, specificStrategy);
        }

        // --- 2. Initialize Contextual Movement Stats ---
        foreach (var modeEntry in archetype.ContextualProfiles)
        {
            var mode = modeEntry.Key;
            var profile = modeEntry.Value;
            this._contextualStats[mode] = new Dictionary<Attribute, IModifiableProperty>();

            foreach (var attrEntry in profile.Attributes)
            {
                var attribute = attrEntry.Key;
                var baseValue = attrEntry.Value;

                // Check if the VelocityProfile provides a specific strategy override for this attribute.
                profile.AttributeStrategies.TryGetValue(attribute, out var specificStrategy);

                _contextualStats[mode][attribute] = GetAttributeStrategy(attribute, baseValue, specificStrategy);
            }
        }

        // 3. Initialize Mechanic Library
        foreach (var (mechanicType, mechanicData) in archetype.MechanicLibrary)
        {
            _mechanicLibrary[mechanicType] = mechanicData;
        }

        // --- 3. Post-Initialization Notification ---
        // Emit an initial change event for all stats so listening systems can sync their initial state.
        foreach (var stat in this._universalStats)
        {
            OnStatChanged?.Invoke(stat.Key, stat.Value.GetValueAsVariant());
        }
    }

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
    public ModifiableProperty<T> GetStat<T>(Attribute attribute, StatContext? context = null)
    {
        // First, attempt to find the most specific, contextual version of the stat.
        if (context != null && this._contextualStats.TryGetValue(context, out var modeStats) &&
            modeStats.TryGetValue(attribute, out var contextualProp))
        {
            if (contextualProp is ModifiableProperty<T> typedProp)
            {
                return typedProp;
            }
            JmoLogger.LogAndRethrow(
                new InvalidCastException($"value for attribute {attribute} is not of type {typeof(T)}"),
                this
            );
        }

        // If no contextual version exists, fall back to the universal version.
        if (this._universalStats.TryGetValue(attribute, out var universalProp))
        {
            if (universalProp is ModifiableProperty<T> typedProp)
            {
                return typedProp;
            }
            JmoLogger.LogAndRethrow(
                new InvalidCastException($"value for attribute {attribute} is not of type {typeof(T)}"),
                this
            );
            // // TODO: replace with jmo logger
            // // This indicates a logic error somewhere in the code or a design error in the data.
            // // Log a detailed error to help developers debug it quickly.
            // GD.PrintErr($"Stat Type Mismatch: Failed to get value for attribute '{attribute?.AttributeName}'. ",
            //     $"Requested type '{typeof(T).Name}' but the stat's actual type is '{prop.Value.VariantType}'. ",
            //     $"Actual stat value is '{prop.Value}'. ",
            //     "Returning default value.");
        }

        // The entity does not possess this stat in any context.
        throw JmoLogger.LogAndRethrow(
            new KeyNotFoundException($"could not find attribute '{attribute.AttributeName}' in the stat controller!"),
            this
            );
            //return null;
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
    public T GetStatValue<[MustBeVariant] T>(Attribute attribute, StatContext? context = null,
        T defaultValue = default(T))
    {
        // TODO: make and use trygetstat
        var prop = GetStat<T>(attribute, context);

        return prop.Value;

        // The stat was not found in any context.
        return defaultValue;
    }

    public T GetMechanicData<T>(MechanicType mechanicType) where T : MechanicData
    {
        if (_mechanicLibrary.TryGetValue(mechanicType, out var data))
        {
            return data as T;
        }
        // todo: throw instead
        return null;
    }

    /// <summary>
    /// The "Gatekeeper" method. It takes a generic modifier resource and safely applies it
    /// to the correct, strongly-typed ModifiableProperty using the 'dynamic' keyword.
    /// </summary>
    public bool TryAddModifier(Attribute attribute, Resource modifierResource, StatContext context = null)
    {
        // TODO: add to context if given

        if (!_universalStats.TryGetValue(attribute, out var prop))
        {
            JmoLogger.Error(this, $"StatController: Attempted to add modifier to non-existent attribute '{attribute.AttributeName}'.");
            return false;
        }

        if (!prop.TryAddModifier(modifierResource))
        {
            JmoLogger.Error(this, $"StatController: Attempted to add modifier of incompatible type '{modifierResource.GetType().FullName}' for attribute '{attribute.AttributeName}'.");
            return false;
        }

        return true;

        // try
        // {
        //     // The 'dynamic' keyword defers the type check until runtime.
        //     // It will attempt to call prop.AddModifier(modifierResource).
        //     // If the generic types of the property (e.g., <float>) and the modifier
        //     // (e.g., IModifier<float>) match, it will succeed.
        //     // If they do not match, it will throw a RuntimeBinderException, which we catch.
        //     dynamic typedProp = prop;
        //     typedProp.AddModifier(modifierResource);
        //     return true;
        // }
        // catch (RuntimeBinderException ex)
        // {
        //     JmoLogger.Info(this,
        //         $"attempted modifier: {modifierResource.GetType().FullName}." +
        //         $"\nNeeded modifier: {prop.GetType().FullName}");
        //     // This catch block is our runtime type validation.
        //     // It means the modifier's type was incompatible with the stat's type.
        //     JmoLogger.Error(this, $"Type Mismatch: Failed to add modifier '{modifierResource.ResourcePath}' to attribute '{attribute.AttributeName}'. The modifier's type is incompatible with the attribute's internal type. Details: {ex.Message}");
        //     return false;
        // }
    }

    /// <summary>
    /// The "Gatekeeper" method. It takes a generic modifier resource and safely applies it
    /// to the correct, strongly-typed ModifiableProperty using the 'dynamic' keyword.
    /// </summary>
    public bool TryRemoveModifier(Attribute attribute, Resource modifierResource, StatContext context = null)
    {
        // TODO: add to context if given

        if (!_universalStats.TryGetValue(attribute, out var prop))
        {
            JmoLogger.Error(this,
                $"StatController: Attempted to add modifier to non-existent attribute '{attribute.AttributeName}'.");
            return false;
        }

        if (!prop.TryRemoveModifier(modifierResource))
        {
            JmoLogger.Error(this,
                $"StatController: Attempted to remove modifier of type '{modifierResource.GetType().FullName}' for attribute '{attribute.AttributeName}'.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the attribute value's Type
    /// </summary>
    /// <param name="attribute"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public Type GetAttributeType(Attribute attribute)
    {
        // First, attempt to find the most specific, contextual version of the stat.
        // If no contextual version exists, fall back to the universal version.
        if (_universalStats.TryGetValue(attribute, out var universalProp))
        {
            return universalProp.GetValueAsVariant().Obj!.GetType();
        }

        throw new InvalidOperationException(); // TODO: define better
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="attribute"></param>
    /// <param name="baseValue"></param>
    /// <param name="specificStrategy"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>

    private IModifiableProperty GetAttributeStrategy(Attribute attribute, Variant baseValue, Resource? specificStrategy = null)
    {
        switch (baseValue.VariantType)
        {
            case Variant.Type.Float:
                switch (specificStrategy)
                {
                    case null:
                        return new ModifiableProperty<float>(
                            baseValue.AsSingle(),
                            _defaultFloatCalcStrat);
                    case ICalculationStrategy<float> floatStrat:
                        return new ModifiableProperty<float>(
                            baseValue.AsSingle(),
                            floatStrat);
                    default:
                        throw JmoLogger.LogAndRethrow(new InvalidCastException($"Incorrect Strategy Type: '{specificStrategy.GetType().Name}' for Variant type '{baseValue.VariantType}'"),
                            this);
                }
                break;
            case Variant.Type.Bool:
                switch (specificStrategy)
                {
                    case null:
                        return new ModifiableProperty<bool>(
                            baseValue.AsBool(),
                            _defaultBoolCalcStrat);
                        break;
                    case ICalculationStrategy<bool> boolStrat:
                        return new ModifiableProperty<bool>(
                            baseValue.AsBool(),
                            boolStrat);
                    default:
                        throw JmoLogger.LogAndRethrow(new InvalidCastException($"Incorrect Strategy Type: '{specificStrategy.GetType().Name}' for Variant type '{baseValue.VariantType}'"),
                            this);
                }
            default:
                throw JmoLogger.LogAndRethrow(new InvalidCastException($"StatController: No handler for attribute '{attribute.AttributeName}' of type '{baseValue.VariantType}'."),
                    this);
        }
    }
}
