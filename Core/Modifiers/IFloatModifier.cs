namespace Jmodot.Core.Modifiers;

/// <summary>
///     A specialized version of the modifier interface specifically for float values,
///     which adds the concept of a calculation stage for a robust mathematical pipeline.
/// </summary>
public interface IFloatModifier : IModifier<float>
{
    CalculationStage Stage { get; }
}
