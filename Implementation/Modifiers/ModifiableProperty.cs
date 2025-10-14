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

    public ModifiableProperty(T baseValue, ICalculationStrategy<T> calculationStrategy)
    {
        BaseValue = baseValue;
        _cachedValue = baseValue;
        _calculationStrategy = calculationStrategy;
    }

    public T BaseValue { get; set; }
    public virtual T Value => GetValue();

    /// <summary>
    /// The core internal method for adding a modifier. It creates a new entry, assigns it a
    /// unique Guid, and returns that Guid to the calling StatController.
    /// </summary>
    /// <returns>The unique Guid generated for this modifier application.</returns>
    public Guid AddModifier(IModifier<T> modifier, object owner)
    {
        var newEntry = new ModifierEntry(modifier, owner);
        _modifierEntries.Add(newEntry);
        _isDirty = true;
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
        }
    }

    /// <summary>
    /// Performs a declarative, bulk removal of all modifiers from a specific source.
    /// This is the primary mechanism for cleaning up effects from states, equipment, or buffs.
    /// </summary>
    public void RemoveAllModifiersFromSource(object owner)
    {
        var removedCount = _modifierEntries.RemoveAll(entry => entry.Owner.Equals(owner));
        if (removedCount > 0)
        {
            _isDirty = true;
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
            foreach (var tag in mod.CancelsEffectTags)
            {
                tagsToCancel.Add(tag);
            }
        }

        var postCancelledMods = sortedModifiers.Where(mod => !(mod.EffectTags?.Any(tagsToCancel.Contains) ?? false)).ToList();

        HashSet<string> collectedContextTags = [];
        List<IModifier<T>> toRemoveMods = [];
        foreach (var mod in postCancelledMods)
        {
            foreach (var tag in mod.ContextTags)
            {
                collectedContextTags.Add(tag);
            }
        }

        var postRequiredMods =
            // keep any mods where any of their required context tags exist in the collected tag hashset
            postCancelledMods.Where(m =>
                m.RequiredContextTags.Count == 0
                ||
                m.RequiredContextTags.Any(collectedContextTags.Contains)).ToList();

        return postRequiredMods;
    }

    #region Interface Implementation
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
    #endregion
}
