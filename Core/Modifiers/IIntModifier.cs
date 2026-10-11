namespace Jmodot.Core.Modifiers;

using StageRules;

/// <summary>
///     A specialized modifier interface for int values. Carries a raw <see cref="Value" /> and a
///     data-driven <see cref="StageRule" /> that the calculation strategy folds by.
/// </summary>
public interface IIntModifier : IModifier<int>
{
    /// <summary>The fold rule. Null when an authored slot is unset; the calculation strategy then drops this modifier with a warning.</summary>
    IntModifierStageRule? StageRule { get; }
    int Value { get; }
}
