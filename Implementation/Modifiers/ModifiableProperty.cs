namespace Jmodot.Implementation.Modifiers;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Stats;
using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.CalculationStrategies;
using Shared;

/// <summary>
/// The runtime representation of a single, modifiable character stat (e.g., MaxSpeed).
/// This class is the definitive manager for a stat's base value and its list of active modifiers.
/// It is responsible for calculating the final value on demand by applying modifiers through a
/// defined calculation strategy.
/// </summary>
/// <typeparam name="T">The underlying type of the stat's value (e.g., float, bool).</typeparam>
public class ModifiableProperty<T> : IModifiableProperty
{
    /// <summary>
    /// An internal structure that atomically bundles a modifier instance with its metadata:
    /// the source that applied it (owner) and a unique ID for its specific application.
    /// </summary>
    private readonly struct ModifierEntry
    {
        public readonly IModifier<T> Modifier;
        public readonly object Owner;
        public readonly Guid Id;

        public ModifierEntry(IModifier<T> modifier, object owner)
        {
            Modifier = modifier;
            Owner = owner;
            Id = Guid.NewGuid();
        }
    }

    private readonly ICalculationStrategy<T> _calculationStrategy;

    private readonly List<ModifierEntry> _modifierEntries = new();
    protected T _cachedValue;
    protected bool _isDirty = true;

    public event Action<Variant> OnValueChanged = delegate { };

    public ModifiableProperty(T baseValue, ICalculationStrategy<T> calculationStrategy)
    {
        _baseValue = baseValue;
        _cachedValue = baseValue;
        _calculationStrategy = calculationStrategy;
    }

    private T _baseValue;
    public T BaseValue
    {
        get => _baseValue;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_baseValue, value)) { return; }
            _baseValue = value;
            _isDirty = true;
            CheckValueChanged();
        }
    }
    public virtual T Value => GetValue();

    /// <summary>
    /// The core internal method for adding a modifier. It creates a new entry, assigns it a
    /// unique Guid, and returns that Guid to the calling StatController.
    /// </summary>
    /// <returns>The unique Guid generated for this modifier application.</returns>
    public Guid AddModifier(IModifier<T> modifier, object owner)
    {
        if (modifier == null) { throw new ArgumentNullException(nameof(modifier)); }
        if (owner == null) { throw new ArgumentNullException(nameof(owner)); }

        var newEntry = new ModifierEntry(modifier, owner);
        _modifierEntries.Add(newEntry);
        _isDirty = true;
        CheckValueChanged();
        return newEntry.Id;
    }

    /// <summary>
    /// Performs a precise removal of a single modifier application using its unique ID.
    /// </summary>
    public void RemoveModifier(Guid modifierId)
    {
        var removedCount = _modifierEntries.RemoveAll(entry => entry.Id == modifierId);
        if (removedCount > 0)
        {
            _isDirty = true;
            CheckValueChanged();
        }
    }

    /// <summary>
    /// Performs a declarative, bulk removal of all modifiers from a specific source.
    /// This is the primary mechanism for cleaning up effects from states, equipment, or buffs.
    /// </summary>
    public void RemoveAllModifiersFromSource(object owner)
    {
        var removedCount = _modifierEntries.RemoveAll(entry => Equals(entry.Owner, owner));
        if (removedCount > 0)
        {
            _isDirty = true;
            CheckValueChanged();
        }
    }

    protected virtual T GetValue()
    {
        if (!_isDirty)
        {
            return _cachedValue;
        }

        var finalModifiers = GetFinalModifiers(); // Use the powerful filtering helper

        // Delegate the calculation to the strategy
        _cachedValue = _calculationStrategy.Calculate(BaseValue, finalModifiers);

        _isDirty = false;
        return _cachedValue;
    }

    protected List<IModifier<T>> GetFinalModifiers()
    {
        if (_modifierEntries.Count == 0)
        {
            return new List<IModifier<T>>();
        }
        var modifiers = _modifierEntries.Select(entry => entry.Modifier);
        var sortedModifiers = modifiers.OrderByDescending(m => m.Priority).ToList();
        var tagsToCancel = new HashSet<string>();
        foreach (var mod in sortedModifiers)
        {
            if (mod.CancelsEffectTags == null) { continue; }
            foreach (var tag in mod.CancelsEffectTags)
            {
                tagsToCancel.Add(tag);
            }
        }

        var postCancelledMods = sortedModifiers.Where(mod => !(mod.EffectTags?.Any(tagsToCancel.Contains) ?? false)).ToList();

        HashSet<string> collectedContextTags = [];
        foreach (var mod in postCancelledMods)
        {
            if (mod.ContextTags == null) { continue; }
            foreach (var tag in mod.ContextTags)
            {
                collectedContextTags.Add(tag);
            }
        }

        var postRequiredMods =
            // keep any mods where any of their required context tags exist in the collected tag hashset
            postCancelledMods.Where(m =>
                (m.RequiredContextTags == null || m.RequiredContextTags.Count == 0)
                ||
                m.RequiredContextTags.Any(collectedContextTags.Contains)).ToList();

        return postRequiredMods;
    }

    protected void CheckValueChanged()
    {
        // We need to calculate the new value to see if it actually changed.
        // We can't just rely on _isDirty because adding a modifier might not change the final value
        // (e.g. +0 damage, or a modifier that gets cancelled).

        // Save the old value
        T oldValue = _cachedValue;

        // Force a recalculation
        T newValue = GetValue();

        // Compare
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            OnValueChanged?.Invoke(Variant.From(newValue));
        }
    }

    #region Interface Implementation
    /// <summary>
    /// Creates a deep copy of this property's state.
    /// It preserves the BaseValue, Strategy, and exact Modifier IDs.
    /// </summary>
    public IModifiableProperty Clone()
    {
        // 1. Create the new instance with the same BaseValue and Strategy
        // Note: Strategies are typically stateless logic classes, so sharing the reference is correct.
        var clone = new ModifiableProperty<T>(this._baseValue, this._calculationStrategy);

        // 2. Deep Copy Modifiers (Preserving IDs)
        // We access the private '_modifierEntries' of the clone directly.
        // Since ModifierEntry is a struct, adding it creates a copy of the data (ID, Owner ref, Modifier ref).
        // Preserving the Guid is critical so that cloned ModifierHandles remain valid.
        foreach (var entry in this._modifierEntries)
        {
            clone._modifierEntries.Add(entry);
        }

        // 3. Copy Optimization State
        // We copy the cache state so the clone doesn't need to recalculate immediately
        // if the original was already clean.
        clone._cachedValue = this._cachedValue;
        clone._isDirty = this._isDirty;

        return clone;
    }
    public Variant GetValueAsVariant()
    {
        return Variant.From(Value);
    }
    // This region acts as a type-safe bridge between the generic class and the non-generic interface.
    Guid IModifiableProperty.AddModifier(Resource modifierResource, object owner)
    {
        if (modifierResource is IModifier<T> typedModifier)
        {
            return AddModifier(typedModifier, owner);
        }
        throw JmoLogger.LogAndRethrow(new InvalidCastException($"Resource of type {GetType().Name} is not of type {nameof(IModifier<T>)}, cannot cast to modifier for this property!"),
            this);
    }

    public void TransferModifiersTo(IModifiableProperty target)
    {
        foreach (var entry in _modifierEntries)
        {
            // We use the generic AddModifier via the interface to handle type-casting correctly.
            // Note: We use the existing Modifier resource and its original Owner.
            // This ensures that the target property now has a "copy" of the modifier application.
            if (entry.Modifier is Resource resource)
            {
                target.AddModifier(resource, entry.Owner);
            }
            else
            {
                JmoLogger.Warning(typeof(ModifiableProperty<T>),
                    $"Cannot transfer modifier of type {entry.Modifier.GetType().Name} — not a Resource");
            }
        }
    }

    public void SetBaseValue(Variant newBaseValue)
    {
        // Convert the Variant to the expected type T
        // IMPORTANT: Godot stores all floats as doubles internally, so Variant.Obj returns double
        // for float types. We must use type-specific conversion methods instead of pattern matching.
        T convertedValue;
        try
        {
            if (typeof(T) == typeof(float))
            {
                // Godot's Variant stores floats as doubles, use AsSingle() to convert
                convertedValue = (T)(object)newBaseValue.AsSingle();
            }
            else if (typeof(T) == typeof(double))
            {
                convertedValue = (T)(object)newBaseValue.AsDouble();
            }
            else if (typeof(T) == typeof(int))
            {
                convertedValue = (T)(object)newBaseValue.AsInt32();
            }
            else if (typeof(T) == typeof(bool))
            {
                convertedValue = (T)(object)newBaseValue.AsBool();
            }
            else if (typeof(T) == typeof(string))
            {
                convertedValue = (T)(object)newBaseValue.AsString();
            }
            else if (newBaseValue.Obj is T tVal)
            {
                // Fallback for complex types (Vectors, Resources, etc.)
                convertedValue = tVal;
            }
            else
            {
                JmoLogger.Error(this, $"Attempted to set base value for property of type {typeof(T).Name}, but variant value is of type {newBaseValue.Obj?.GetType().Name}.");
                return;
            }
        }
        catch (Exception ex)
        {
            JmoLogger.Error(this, $"Failed to convert Variant to {typeof(T).Name}: {ex.Message}");
            return;
        }

        BaseValue = convertedValue;
    }
    #endregion
}
