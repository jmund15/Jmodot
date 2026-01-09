namespace Jmodot.Core.Stats;

using System;
using System.Collections.Generic;
using System.Text;
using Implementation.Modifiers.CalculationStrategies;
using Implementation.Registry;
using Implementation.Shared;
using Mechanics;
using Microsoft.CSharp.RuntimeBinder;
using Modifiers;
using Modifiers.CalculationStrategies;
using Jmodot.Implementation.Modifiers;
using PushinPotions.Global;
using Shared;

/// <summary>
///     The definitive runtime "character sheet" and single source of truth for all of an entity's
///     dynamic properties. This class manages both universal stats (like Max Health) and contextual
///     stats (like Ground Speed), applying modifiers and calculating the final values on demand.
///     It is initialized from a EntityStatSheet and serves as the central hub for any system
///     needing to query or modify character data.
/// </summary>
[GlobalClass]
public partial class StatController : Node, IStatProvider, IRuntimeCopyable<StatController>
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
    private readonly IntCalculationStrategy _defaultIntCalcStrat = new();
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
    public void InitializeFromStatSheet(EntityStatSheet archetype)
    {
        _archetype = archetype;

        // --- 1. Initialize Universal Stats ---
        foreach (var entry in archetype.UniversalAttributes)
        {
            var attribute = entry.Key;
            var baseValue = entry.Value;

            // Look up the specific strategy assigned in the archetype for this attribute.
            archetype.UniversalAttributeStrategies.TryGetValue(attribute, out var specificStrategy);

            var prop = GetAttributeStrategy(attribute, baseValue, specificStrategy);
            _stats[attribute] = prop;

            // Subscribe to the property's internal change event so we can forward it
            // to the central OnStatChanged event and any specific subscribers.
            prop.OnValueChanged += (newValue) => NotifySubscribers(attribute, newValue);
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

    private readonly Dictionary<Attribute, Action<Variant>> _subscriptions = new();

    public void Subscribe(Attribute attribute, Action<Variant> callback)
    {
        if (_subscriptions.ContainsKey(attribute))
        {
            _subscriptions[attribute] += callback;
        }
        else
        {
            _subscriptions[attribute] = callback;
        }
    }

    public void Unsubscribe(Attribute attribute, Action<Variant> callback)
    {
        if (_subscriptions.ContainsKey(attribute))
        {
            _subscriptions[attribute] -= callback;
            if (_subscriptions[attribute] == null)
            {
                _subscriptions.Remove(attribute);
            }
        }
    }

    private void NotifySubscribers(Attribute attribute, Variant newValue)
    {
        // 1. Invoke the global event (legacy support + global listeners)
        OnStatChanged?.Invoke(attribute, newValue);

        // 2. Invoke specific subscribers
        if (_subscriptions.TryGetValue(attribute, out var callback))
        {
            callback?.Invoke(newValue);
        }
    }

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

    public void LogAllStatValues()
    {
        StringBuilder sb = new();
        sb.AppendLine("Stats:");
        foreach (var (attr, value) in _stats)
        {
            sb.AppendLine($"\tattr: {attr.AttributeName}, value: {value.GetValueAsVariant()}");
        }
        JmoLogger.Info(this, sb.ToString());
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
    /// Merges stats and modifiers from another controller into this one.
    /// If an attribute exists in both, only modifiers are transferred.
    /// If it only exists in the source, the whole property (Base + Strategy + Modifiers) is copied.
    /// </summary>
    public void InheritModifiers(StatController source)
    {
        foreach (var (attribute, sourceProp) in source._stats)
        {
            if (!_stats.TryGetValue(attribute, out var targetProp))
            {
                // Case 1: Attribute doesn't exist here. Clone the whole thing.
                var clonedProp = sourceProp.Clone();
                _stats[attribute] = clonedProp;
                clonedProp.OnValueChanged += (newValue) => NotifySubscribers(attribute, newValue);
            }
            else
            {
                // Case 2: Attribute exists. Transfer ONLY modifiers.
                sourceProp.TransferModifiersTo(targetProp);
            }
        }
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
        if (TryAddModifier(attribute, modifierResource, owner, out var handle))
        {
            return handle!;
        }

        // DEBUG: Logging to diagnose why TryAddModifier returned false
        var sb = new StringBuilder();
        sb.AppendLine($"[StatController] FAILED to add modifier '{modifierResource.ResourceName}' to attribute '{attribute.AttributeName}' (ID: {attribute.GetInstanceId()})");
        sb.AppendLine($"Available Attributes in _stats ({_stats.Count}):");
        foreach (var key in _stats.Keys)
        {
            sb.AppendLine($" - '{key.AttributeName}' (ID: {key.GetInstanceId()})");
        }
        JmoLogger.Error(this, sb.ToString());

        throw JmoLogger.LogAndRethrow(
            new InvalidOperationException(
                $"unable to add modifier {modifierResource.ResourcePath} to attribute {attribute.AttributeName}"),
            this
        );
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
            if (handle == null)
            {
                return;
            }
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
    /// Sets the base value of an attribute without removing any active modifiers.
    /// Modifiers will be recalculated on top of the new base value.
    /// </summary>
    /// <typeparam name="T">The type of the attribute's value.</typeparam>
    /// <param name="attribute">The attribute to modify.</param>
    /// <param name="newValue">The new base value.</param>
    public void SetBaseValue(Attribute attribute, Variant newValue)
    {
        if (_stats.TryGetValue(attribute, out var property))
        {
            property.SetBaseValue(newValue);
        }
        else
        {
            JmoLogger.Warning(this, $"Attempted to set base value for unknown attribute '{attribute?.AttributeName}'");
        }
    }
    // public void SetBaseValue<[MustBeVariant] T>(Attribute attribute, T newValue)
    // {
    //     if (_stats.TryGetValue(attribute, out var property))
    //     {
    //         property.SetBaseValue(Variant.From(newValue));
    //     }
    //     else
    //     {
    //         JmoLogger.Warning(this, $"Attempted to set base value for unknown attribute '{attribute?.AttributeName}'");
    //     }
    // }

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
            case Variant.Type.Int:
                switch (specificStrategy)
                {
                    case null:
                        return new ModifiableProperty<int>(
                            baseValue.AsInt32(),
                            _defaultIntCalcStrat
                        );
                    case  ICalculationStrategy<int> intStrat:
                        return new ModifiableProperty<int>(
                            baseValue.AsInt32(),
                            intStrat);
                    default:
                        throw JmoLogger.LogAndRethrow(new InvalidCastException($"Incorrect Strategy Type: '{specificStrategy.GetType().Name}' for Variant type '{baseValue.VariantType}'"),
                            this);
                }
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

    /// <summary>
    /// Performs a deep copy of the runtime state from the original controller.
    /// This includes current stat values, active modifiers, mechanic data, and active contexts.
    /// External subscriptions (Listeners) are NOT copied.
    /// </summary>
    public void CopyStateFrom(StatController original)
    {
        // 1. Copy Configuration
        _archetype = original._archetype; // StatSheet is a Resource (Shared)

        // 2. Clear current state to ensure a clean slate
        _stats.Clear();
        _mechanicLibrary.Clear();
        _activeContexts.Clear();
        // Note: We do NOT clear _subscriptions here, as this instance might be new
        // and already have its own listeners attached via _Ready().

        // 3. Deep Copy Stats
        foreach (var kvp in original._stats)
        {
            Attribute attribute = kvp.Key;
            IModifiableProperty originalProp = kvp.Value;

            // Clone the property to copy its BaseValue, Strategy, and current Modifiers.
            IModifiableProperty newProp = originalProp.Clone();

            // CRITICAL: Re-wire the internal event listener.
            // The cloned property doesn't know about this new StatController.
            newProp.OnValueChanged += (newValue) => NotifySubscribers(attribute, newValue);

            _stats[attribute] = newProp;
        }

        // 4. Deep Copy Mechanics
        foreach (var kvp in original._mechanicLibrary)
        {
            MechanicType type = kvp.Key;
            MechanicData originalData = kvp.Value;

            // Duplicate the Resource with sub-resources (true) to ensure
            // runtime changes to the mechanic (e.g., Cooldowns, Ammo) are unique to this clone.
            if (originalData.Duplicate(true) is MechanicData newData)
            {
                _mechanicLibrary[type] = newData;
            }
        }

        // 5. Copy Active Contexts
        // Contexts act as tags/flags, so shallow copying the list is sufficient
        // (Assuming StatContext is a shared Resource)
        foreach (var context in original._activeContexts)
        {
            _activeContexts.Add(context);
        }

        // 6. Notify Initial State
        // Let any listeners attached to *this* controller know the values have been updated.
        // NOTE: we have no way to know all the subscribers to the original controller, so we can't connect them here
        // Normally this is not desired behavior anyway.
        foreach (var stat in _stats)
        {
            NotifySubscribers(stat.Key, stat.Value.GetValueAsVariant());
        }
    }
}
