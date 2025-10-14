namespace Jmodot.Core.Stats;

using System;
using System.Collections.Generic;
using Implementation.Modifiers.CalculationStrategies;
using Implementation.Registry;
using Implementation.Shared;
using Mechanics;
using Microsoft.CSharp.RuntimeBinder;
using Modifiers;
using Modifiers.CalculationStrategies;
using Jmodot.Implementation.Modifiers;
using PushinPotions.Global;

/// <summary>
///     The definitive runtime "character sheet" and single source of truth for all of an entity's
///     dynamic properties. This class manages both universal stats (like Max Health) and contextual
///     stats (like Ground Speed), applying modifiers and calculating the final values on demand.
///     It is initialized from a EntityStatSheet and serves as the central hub for any system
///     needing to query or modify character data.
/// </summary>
[GlobalClass]
public partial class StatController : Node, IStatProvider
{
    /// <summary>
    ///     Stores all universal stats that apply to the entity regardless of its state.
    ///     Example: Max Health, Strength, Intelligence.
    /// </summary>
    private readonly Dictionary<Attribute, IModifiableProperty> _stats = new();
    // A dedicated library for mechanics.
    // Buffs/debuffs can change a mechanic's data at runtime (e.g., a "Jump Power" Attribute).
    private readonly Dictionary<MechanicType, MechanicData> _mechanicLibrary = new();

    // Default strategies (can make static if we make the get calc strat static)
    private readonly FloatCalculationStrategy _defaultFloatCalcStrat = new();
    private readonly BoolOverrideStrategy _defaultBoolCalcStrat = new();

    private readonly HashSet<StatContext> _activeContexts = new();

    private EntityStatSheet _archetype = null!;

    // --- Initialization ---

    /// <summary>
    ///     Configures and populates the entire StatController based on the data defined in a
    ///     EntityStatSheet resource. This method builds the runtime ModifiableProperty objects,
    ///     assigns the correct calculation strategies, and sets their base values.
    /// </summary>
    /// <param name="archetype">The data template used to define this entity's stats.</param>
    public void InitializeFromArchetype(EntityStatSheet archetype)
    {
        _archetype = archetype;

        // --- 1. Initialize Universal Stats ---
        foreach (var entry in archetype.UniversalAttributes)
        {
            var attribute = entry.Key;
            var baseValue = entry.Value;

            // Look up the specific strategy assigned in the archetype for this attribute.
            archetype.UniversalAttributeStrategies.TryGetValue(attribute, out var specificStrategy);

            _stats[attribute] = GetAttributeStrategy(attribute, baseValue, specificStrategy);
        }


        // --- 2. Initialize Mechanic Library ---
        foreach (var (mechanicType, mechanicData) in archetype.MechanicLibrary)
        {
            _mechanicLibrary[mechanicType] = mechanicData;
        }

        // --- 3. Post-Initialization Notification ---
        // Emit an initial change event for all stats so listening systems can sync their initial state.
        foreach (var stat in this._stats)
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
    public ModifiableProperty<T> GetStat<T>(Attribute attribute)
    {
        // If no contextual version exists, fall back to the universal version.
        if (this._stats.TryGetValue(attribute, out var prop))
        {
            if (prop is ModifiableProperty<T> typedProp)
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
    /// <param name="defaultValue">The value to return if the stat doesn't exist or if a type mismatch occurs.</param>
    /// <returns>The final calculated value of the stat, or the default value on failure.</returns>
    public T GetStatValue<[MustBeVariant] T>(Attribute attribute, T defaultValue = default(T))
    {
        if (_stats.TryGetValue(attribute, out var prop))
        {
            if (prop is ModifiableProperty<T> typedProp)
            {
                return typedProp.Value;
            }
        }
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

    public bool TryAddModifier(Attribute attribute, Resource modifierResource, object owner, out ModifierHandle? handle)
    {
        if (_stats.TryGetValue(attribute, out var property))
        {
            // 1. Delegate the actual modification to the specialized property.
            // It returns a unique internal ID for this specific application.
            Guid newId = property.AddModifier(modifierResource, owner);

            if (newId != Guid.Empty)
            {
                // 2. As the Facade, the StatController is responsible for creating the
                // public-facing handle, packaging the internal ID with the routing
                // information (the property itself). This is the correct separation of concerns.
                handle = new ModifierHandle(property, newId);
                return true;
            }
        }
        handle = null;
        return false; // Indicates failure
    }
    public ModifierHandle AddModifier(Attribute attribute, Resource modifierResource, object owner)
    {
        if (TryAddModifier(attribute, modifierResource, owner, out var handle))
        {
            return handle!;
        }

        throw JmoLogger.LogAndRethrow(
            new InvalidOperationException(
                $"unable to add modifier {modifierResource.ResourcePath} to attribute {attribute.AttributeName}"),
            this
        );
    }
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

        public void RemoveModifier(ModifierHandle handle)
        {
            if (handle == null) return;

            // The handle provides all the necessary information for a direct, efficient removal.
            // We know exactly which property to talk to and which ID to remove. No searching is needed.
            handle.Property.RemoveModifier(handle.ModifierId);
        }

        public void RemoveAllModifiersFromSource(object owner)
        {
            // Broadcast the declarative cleanup request to all properties. Each property
            // is responsible for checking its own list and removing relevant modifiers.
            foreach (var property in _stats.Values)
            {
                property.RemoveAllModifiersFromSource(owner);
            }
        }
        public void AddActiveContext(StatContext context)
        {
            _activeContexts.Add(context);
            if (GlobalRegistry.DB.StatContextProfiles.TryGetValue(context, out var profile))
            {
                foreach (var (attribute, modifier) in profile.Modifiers)
                {
                    // The context resource itself acts as the owner. We don't need to store the
                    // returned handle because we'll use the declarative RemoveActiveContext for cleanup.
                    AddModifier(attribute, modifier, context);
                }
            }
        }

        public void RemoveActiveContext(StatContext context)
        {
            _activeContexts.Remove(context);
            // This is a simple, powerful shorthand for the most common cleanup operation.
            RemoveAllModifiersFromSource(context);
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
        if (_stats.TryGetValue(attribute, out var universalProp))
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
