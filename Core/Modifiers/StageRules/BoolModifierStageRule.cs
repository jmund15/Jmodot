namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>
///     Data-driven fold rule for one stage of a bool modifier pipeline. No NeutralValue — efficacy
///     scaling never touches bool stats. See <see cref="FloatModifierStageRule" /> for the fold contract.
/// </summary>
[GlobalClass]
public abstract partial class BoolModifierStageRule : Resource
{
    /// <summary>Stage identity. Modifiers sharing a StageId fold together in one <see cref="Reduce" /> call.</summary>
    [Export] public StringName StageId { get; protected set; } = "";

    /// <summary>Fold order across stages. Lower runs first.</summary>
    [Export] public int Order { get; protected set; }

    /// <summary>
    ///     Folds this stage's modifier values onto the running result.
    ///     <paramref name="stageValues" /> arrives PRE-SORTED by modifier Priority descending.
    /// </summary>
    public abstract bool Reduce(bool running, IReadOnlyList<bool> stageValues);
}
