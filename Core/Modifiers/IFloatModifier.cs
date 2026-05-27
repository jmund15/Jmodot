namespace Jmodot.Core.Modifiers;

using StageRules;

/// <summary>
///     A specialized modifier interface for float values. Carries a raw <see cref="Value" /> and a
///     data-driven <see cref="StageRule" /> that the calculation strategy folds by.
/// </summary>
public interface IFloatModifier : IModifier<float>
{
    FloatModifierStageRule StageRule { get; }
    float Value { get; }
}
