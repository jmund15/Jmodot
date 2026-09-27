namespace Jmodot.Core.Modifiers;

using StageRules;

/// <summary>
///     A specialized modifier interface for float values. Carries a raw <see cref="Value" /> and a
///     data-driven <see cref="StageRule" /> that the calculation strategy folds by.
/// </summary>
public interface IFloatModifier : IModifier<float>
{
    /// <summary>The fold rule. Null when an authored slot is unset; the calculation strategy then drops this modifier with a warning.</summary>
    FloatModifierStageRule? StageRule { get; }
    float Value { get; }
}
