namespace Jmodot.Implementation.Modifiers.CalculationStrategies;

using System.Collections.Generic;
using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.CalculationStrategies;

/// <summary>
///     Base Godot Resource class for creating custom calculation strategies.
/// </summary>
/// <remarks>
///     Note: Unsure if we even need the base interface, but if not it's just an extra layer of boilerplate.
/// </remarks>
[GlobalClass]
public partial class VariantDefaultCalculationStrategy : Resource, ICalculationStrategy<Variant>
{
    public virtual Variant Calculate(Variant baseValue, List<IModifier<Variant>> modifiers)
    {
        // For non-numeric types, we assume the highest priority modifier simply overrides the value.
        // The list is already sorted by priority.
        if (modifiers.Count > 0)
        {
            // The 'Modify' method for an override modifier should just return its own value, ignoring input.
            return modifiers[0].Modify(baseValue);
        }

        return baseValue;
    }
}
