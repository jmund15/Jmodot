#region

using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.CalculationStrategy;

#endregion

namespace Jmodot.Implementation.Modifiers;

public class VariantModifiableProperty : ModifiableProperty<Variant>
{
    public VariantModifiableProperty(Variant baseValue, ICalculationStrategy<Variant> calculationStrategy)
        : base(baseValue, calculationStrategy)
    {
    }
}