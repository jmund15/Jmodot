namespace Jmodot.Implementation.Modifiers;

using Core.Modifiers;
using Core.Modifiers.CalculationStrategies;

public class VariantModifiableProperty : ModifiableProperty<Variant>
{
    public VariantModifiableProperty(Variant baseValue, ICalculationStrategy<Variant> calculationStrategy)
        : base(baseValue, calculationStrategy)
    {
    }
}
