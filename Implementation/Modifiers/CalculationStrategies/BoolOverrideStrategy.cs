namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using Godot;
using System.Collections.Generic;
using Core.Stats;
using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.CalculationStrategies;

/// <summary>
/// A concrete Resource that provides a priority-based override strategy for Boolean stats.
/// </summary>
[GlobalClass]
public partial class BoolOverrideStrategy : Resource, ICalculationStrategy<bool>
{
    private readonly GenericOverrideCalculation<bool> _logic = new();

    public bool Calculate(bool baseValue, IReadOnlyList<IModifier<bool>> activeModifiers)
    {
        return _logic.Calculate(baseValue, activeModifiers);
    }
}
