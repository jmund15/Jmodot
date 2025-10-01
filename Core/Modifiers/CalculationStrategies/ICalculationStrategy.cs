namespace Jmodot.Core.Modifiers.CalculationStrategies;

using System.Collections.Generic;

/// <summary>
///     Defines a contract for a swappable strategy that calculates a final
///     value from a base value and a list of modifiers.
/// </summary>
public interface ICalculationStrategy<T>
{
    T Calculate(T baseValue, List<IModifier<T>> modifiers);
}
