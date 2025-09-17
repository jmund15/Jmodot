namespace Jmodot.Core.Modifiers;

using System.Collections.Generic;
using System.Linq;
using CalculationStrategy;

/// <summary>
///     The generic wrapper class for any value that needs to be dynamically modified.
///     It provides the core logic for managing modifiers, caching values, and resolving conflicts
///     via tags and priority. The default calculation applies modifiers in a simple priority-sorted list.
/// </summary>
public class ModifiableProperty<T>
{
    private readonly ICalculationStrategy<T> _calculationStrategy;

    private readonly List<IModifier<T>> _modifiers = new();
    protected T _cachedValue;
    protected bool _isDirty = true;

    public ModifiableProperty(T baseValue, ICalculationStrategy<T> calculationStrategy)
    {
        this.BaseValue = baseValue;
        this._cachedValue = baseValue;
        this._calculationStrategy = calculationStrategy;
    }

    public T BaseValue { get; set; }
    public virtual T Value => this.GetValue();

    public virtual void AddModifier(IModifier<T> modifier)
    {
        this._modifiers.Add(modifier);
        this._isDirty = true;
    }

    public virtual void RemoveModifier(IModifier<T> modifier)
    {
        this._modifiers.Remove(modifier);
        this._isDirty = true;
    }

    protected virtual T GetValue()
    {
        if (!this._isDirty)
        {
            return this._cachedValue;
        }

        var finalModifiers = this.GetFinalModifiers(); // Use the powerful filtering helper

        // Delegate the calculation to the strategy
        this._cachedValue = this._calculationStrategy.Calculate(this.BaseValue, finalModifiers);

        this._isDirty = false;
        return this._cachedValue;
    }

    protected List<IModifier<T>> GetFinalModifiers()
    {
        if (this._modifiers.Count == 0)
        {
            return new List<IModifier<T>>();
        }

        var sortedModifiers = this._modifiers.OrderByDescending(m => m.Priority).ToList();
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
}
