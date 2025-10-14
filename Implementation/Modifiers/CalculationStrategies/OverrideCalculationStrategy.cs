namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using Godot;
using System.Collections.Generic;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;
using Core.Stats;

/// <summary>
/// A non-Resource, generic class that contains the core logic for a priority-based
/// override calculation. This allows us to reuse the logic without violating Godot's
/// rules about generic Resources.
/// </summary>
public class GenericOverrideCalculation<T> : ICalculationStrategy<T>
{
    // TODO: Add universal calculation function for filtering out required contexts
    public T Calculate(T baseValue, IReadOnlyList<IModifier<T>> activeModifiers)
    {
        if (activeModifiers.Count > 0)
        {
            // The list is pre-sorted by priority in ModifiableProperty, so the first one wins.
            return activeModifiers[0].Modify(baseValue);
        }
        return baseValue;
    }
}
