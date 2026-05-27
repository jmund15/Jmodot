namespace Jmodot.Core.Modifiers;

using StageRules;

/// <summary>
///     A specialized modifier interface for int values. Carries a raw <see cref="Value" /> and a
///     data-driven <see cref="StageRule" /> that the calculation strategy folds by.
/// </summary>
public interface IIntModifier : IModifier<int>
{
    IntModifierStageRule StageRule { get; }
    int Value { get; }
}
