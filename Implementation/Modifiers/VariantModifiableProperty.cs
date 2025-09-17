namespace Jmodot.Implementation.Modifiers;

using Core.Modifiers;
using Core.Modifiers.CalculationStrategy;

public class VariantModifiableProperty : ModifiableProperty<Variant>
{
    public VariantModifiableProperty(Variant baseValue, ICalculationStrategy<Variant> calculationStrategy)
        : base(baseValue, calculationStrategy)
    {
    }
}
