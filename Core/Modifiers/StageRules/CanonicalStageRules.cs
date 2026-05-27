namespace Jmodot.Core.Modifiers.StageRules;

/// <summary>
///     Shared, code-side canonical StageRule instances for constructing modifiers in C#
///     (gameplay systems and tests). The stateless rules below are read-only after construction,
///     so a single shared instance per stage is safe to fan out across many modifiers.
///     Bounded rules (Floor/Cap) carry per-instance bounds and must be constructed at the use site.
///     Data-authored modifiers reference the equivalent <c>.tres</c> under
///     <c>Implementation/Modifiers/StageRules/</c>; both fold identically (grouping is by StageId).
/// </summary>
public static class CanonicalStageRules
{
    public static readonly AdditiveStageRule FloatAdditive = new();
    public static readonly SummedPercentStageRule FloatSummedPercent = new();
    public static readonly IndependentMultiplyStageRule FloatMultiply = new();
    public static readonly OverrideStageRule FloatOverride = new();

    public static readonly IntAdditiveStageRule IntAdditive = new();
    public static readonly IntSummedPercentStageRule IntSummedPercent = new();
    public static readonly IntIndependentMultiplyStageRule IntMultiply = new();
    public static readonly IntOverrideStageRule IntOverride = new();
}
