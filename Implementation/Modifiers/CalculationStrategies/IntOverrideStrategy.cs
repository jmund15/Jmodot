namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using Godot;
using System.Collections.Generic;
using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.CalculationStrategies;

/// <summary>
/// A concrete Resource that provides a priority-based override strategy for int stats.
/// Used for enum-backed int stats (RepeatMode, ChargeRelease, ChargeMobility) where
/// the highest-priority modifier's value replaces the base outright.
/// </summary>
[GlobalClass]
public partial class IntOverrideStrategy : Resource, ICalculationStrategy<int>
{
    private readonly GenericOverrideCalculation<int> _logic = new();

    public int Calculate(int baseValue, IReadOnlyList<IModifier<int>> activeModifiers)
    {
        return _logic.Calculate(baseValue, activeModifiers);
    }
}
