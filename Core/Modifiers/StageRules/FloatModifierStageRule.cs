namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>
///     Data-driven fold rule for one stage of a float modifier pipeline. Each modifier carries a
///     reference to a concrete rule; the calculation strategy groups modifiers by <see cref="StageId" />,
///     orders the groups by <see cref="Order" />, and folds each group via <see cref="Reduce" />.
///     Adding a new fold behaviour is a new concrete + a new <c>.tres</c> — no strategy code change.
/// </summary>
[GlobalClass]
public abstract partial class FloatModifierStageRule : Resource
{
    /// <summary>Stage identity. Modifiers sharing a StageId fold together in one <see cref="Reduce" /> call.</summary>
    [Export] public StringName StageId { get; protected set; } = "";

    /// <summary>Fold order across stages. Lower runs first (BaseAdd=100 → … → Cap=500).</summary>
    [Export] public int Order { get; protected set; }

    /// <summary>
    ///     The per-stage identity element (0 for additive/percent, 1 for multiply). Used by efficacy
    ///     scaling to interpolate a modifier's value toward neutrality without a per-stage switch.
    /// </summary>
    [Export] public float NeutralValue { get; protected set; }

    /// <summary>
    ///     Folds this stage's modifier values onto the running result.
    ///     <paramref name="stageValues" /> arrives PRE-SORTED by modifier Priority descending
    ///     (Override depends on this — contract held by the calculation strategy's input).
    /// </summary>
    public abstract float Reduce(float running, IReadOnlyList<float> stageValues);
}
