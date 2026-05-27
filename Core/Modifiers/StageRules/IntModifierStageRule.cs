namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>
///     Data-driven fold rule for one stage of an int modifier pipeline. Mirror of
///     <see cref="FloatModifierStageRule" /> — see it for the grouping/ordering contract.
/// </summary>
[GlobalClass]
public abstract partial class IntModifierStageRule : Resource
{
    /// <summary>Stage identity. Modifiers sharing a StageId fold together in one <see cref="Reduce" /> call.</summary>
    [Export] public StringName StageId { get; protected set; } = "";

    /// <summary>Fold order across stages. Lower runs first.</summary>
    [Export] public int Order { get; protected set; }

    /// <summary>The per-stage identity element (0 for additive/percent, 1 for multiply). Kept for float/int parity.</summary>
    [Export] public int NeutralValue { get; protected set; }

    /// <summary>
    ///     Folds this stage's modifier values onto the running result.
    ///     <paramref name="stageValues" /> arrives PRE-SORTED by modifier Priority descending.
    /// </summary>
    public abstract int Reduce(int running, IReadOnlyList<int> stageValues);
}
