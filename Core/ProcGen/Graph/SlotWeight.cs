namespace Jmodot.Core.ProcGen.Graph;

using Jmodot.Implementation.ProcGen.Graph;

/// <summary>
///     Base for a SOFT placement bias: a stateless rule that scores a candidate
///     <see cref="Placement" /> that already passed all hard constraints. The generator multiplies
///     weights — <c>config.Weights.Aggregate(1L, (a, w) =&gt; a * w.Weight(...))</c> — then does a
///     weighted pick, so a weight must be <c>&gt;= 1</c> (1 = neutral / no bias).
///     <para>
///         Mirrors the <c>TransitionCondition</c> precedent and is <b>stateless by contract</b> —
///         no mutable instance fields beyond <c>[Export]</c>s
///         (see arch_rule_transition_condition_stateless).
///     </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class SlotWeight : Resource
{
    /// <summary>
    ///     SOFT multiplicative bias, <c>&gt;= 1</c>. Side-effect-free — called per-surviving-candidate
    ///     by the generator. Must return <c>1</c> (neutral) rather than throw when its required
    ///     inputs are unavailable (e.g. metrics not yet computed; see <see cref="RequiresMetrics" />)
    ///     or when the placement is outside this rule's concern.
    /// </summary>
    /// <remarks>
    ///     Internal because it consumes the internal mutable builder <see cref="PartialGraph" />;
    ///     polymorphic dispatch happens within the assembly (generator + rule subclasses).
    /// </remarks>
    internal abstract int Weight(in Placement p, PartialGraph g);

    /// <summary>
    ///     True if this rule reads <see cref="IGraphMetrics" /> (critical edges, source-distances).
    ///     The generator filters metrics-required rules out of the pre-Sink spine pass, where the
    ///     metrics snapshot does not yet exist.
    /// </summary>
    public abstract bool RequiresMetrics { get; }
}
