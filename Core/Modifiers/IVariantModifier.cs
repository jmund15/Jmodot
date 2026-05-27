namespace Jmodot.Core.Modifiers;

using StageRules;

/// <summary>
///     A specialized modifier interface for Variant values. Carries a raw <see cref="Value" /> and a
///     data-driven <see cref="StageRule" /> that the calculation strategy folds by.
/// </summary>
public interface IVariantModifier : IModifier<Variant>
{
    VariantModifierStageRule StageRule { get; }
    Variant Value { get; }
}
