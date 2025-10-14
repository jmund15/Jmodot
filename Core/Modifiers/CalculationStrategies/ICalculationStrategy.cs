namespace Jmodot.Core.Modifiers.CalculationStrategies;

using System.Collections.Generic;
using Stats;

/// <summary>
///     Defines a contract for a swappable strategy that calculates a final
///     value from a base value and a list of modifiers.
/// </summary>
public interface ICalculationStrategy<T>
{
    T Calculate(T baseValue, IReadOnlyList<IModifier<T>> activeModifiers);
}
