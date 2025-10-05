namespace Jmodot.Core.Modifiers;

using System.Collections.Generic;
using System.Linq;
using CalculationStrategies;

/// <summary>
///     The generic wrapper class for any value that needs to be dynamically modified.
///     It provides the core logic for managing modifiers, caching values, and resolving conflicts
///     via tags and priority. The default calculation applies modifiers in a simple priority-sorted list.
/// </summary>
public class ModifiableProperty<T> : IModifiableProperty
{
    private readonly ICalculationStrategy<T> _calculationStrategy;

    private readonly List<IModifier<T>> _modifiers = new();
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

    public virtual void AddModifier(IModifier<T> modifier)
    {
        _modifiers.Add(modifier);
        _isDirty = true;
    }

    public virtual void RemoveModifier(IModifier<T> modifier)
    {
        _modifiers.Remove(modifier);
        _isDirty = true;
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
        if (_modifiers.Count == 0)
        {
            return new List<IModifier<T>>();
        }

        var sortedModifiers = _modifiers.OrderByDescending(m => m.Priority).ToList();
        var tagsToCancel = new HashSet<string>();
        foreach (var mod in sortedModifiers)
        {
            if (mod.CancelsTags != null)
            {
                foreach (var tag in mod.CancelsTags)
                {
                    tagsToCancel.Add(tag);
                }
            }
        }

        return sortedModifiers.Where(mod => !(mod.Tags?.Any(tagsToCancel.Contains) ?? false)).ToList();
    }

    public Variant GetValueAsVariant()
    {
        return Variant.From(Value);
    }

    /// <summary>
    /// Explicit implementation of the gatekeeper method.
    /// </summary>
    public bool TryAddModifier(Resource modifierResource)
    {
        // the type-safe cast.
        // check if the provided resource can be cast to the specific
        // generic interface that this property instance requires (e.g., IModifier<float>).
        if (modifierResource is IModifier<T> typedModifier)
        {
            // If the cast is successful, we call the strongly-typed AddModifier method.
            AddModifier(typedModifier);
            return true;
        }

        // If the cast fails, the types are incompatible.
        return false;
    }
}
