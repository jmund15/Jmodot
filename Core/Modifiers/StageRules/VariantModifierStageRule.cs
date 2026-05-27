namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>
///     Data-driven fold rule for one stage of a Variant modifier pipeline. No NeutralValue — efficacy
///     scaling never touches Variant stats. See <see cref="FloatModifierStageRule" /> for the fold contract.
/// </summary>
[GlobalClass]
public abstract partial class VariantModifierStageRule : Resource
{
    /// <summary>Stage identity. Modifiers sharing a StageId fold together in one <see cref="Reduce" /> call.</summary>
    [Export] public StringName StageId { get; protected set; } = "";

    /// <summary>Fold order across stages. Lower runs first.</summary>
    [Export] public int Order { get; protected set; }

    /// <summary>
    ///     Folds this stage's modifier values onto the running result.
    ///     <paramref name="stageValues" /> arrives PRE-SORTED by modifier Priority descending.
    /// </summary>
    public abstract Variant Reduce(Variant running, IReadOnlyList<Variant> stageValues);
}
