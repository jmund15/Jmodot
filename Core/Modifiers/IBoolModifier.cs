namespace Jmodot.Core.Modifiers;

using StageRules;

/// <summary>
///     A specialized modifier interface for bool values. Carries a raw <see cref="Value" /> and a
///     data-driven <see cref="StageRule" /> that the calculation strategy folds by.
/// </summary>
public interface IBoolModifier : IModifier<bool>
{
    BoolModifierStageRule StageRule { get; }
    bool Value { get; }
}
