namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using Godot;
using System.Collections.Generic;
using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;


/// <summary>
/// A non-Resource, generic class that contains the core logic for a priority-based
/// override calculation. This allows us to reuse the logic without violating Godot's
/// rules about generic Resources.
/// </summary>
public class GenericOverrideCalculation<T> : ICalculationStrategy<T>
{
    public T Calculate(T baseValue, List<IModifier<T>> modifiers)
    {
        if (modifiers.Count > 0)
        {
            // The list is pre-sorted by priority in ModifiableProperty, so the first one wins.
            return modifiers[0].Modify(baseValue);
        }
        return baseValue;
    }
}
