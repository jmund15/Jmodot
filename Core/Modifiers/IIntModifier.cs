namespace Jmodot.Core.Modifiers;

/// <summary>
///     A specialized version of the modifier interface specifically for int values,
///     which adds the concept of a calculation stage for a robust mathematical pipeline.
/// </summary>
public interface IIntModifier : IModifier<int>
{
    CalculationStage Stage { get; }
}
