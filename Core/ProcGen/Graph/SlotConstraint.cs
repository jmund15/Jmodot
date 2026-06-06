namespace Jmodot.Core.ProcGen.Graph;

using Jmodot.Implementation.ProcGen.Graph;

/// <summary>
///     Base for a HARD placement filter: a stateless rule that admits or rejects a candidate
///     <see cref="Placement" /> during graph generation. The generator hard-filters candidates with
///     <c>config.Constraints.All(c =&gt; c.IsAdmissible(...))</c> before soft-weighting survivors.
///     <para>
///         Mirrors the <c>TransitionCondition</c> precedent: a <c>[GlobalClass,Tool]</c> Resource
///         with <c>[Export]</c> tuning fields on each subclass and a pure evaluation method.
///         <b>Stateless by contract</b> — no mutable instance fields beyond <c>[Export]</c>s, so a
///         single <c>.tres</c> can be shared across consumers without cross-stomp
///         (see arch_rule_transition_condition_stateless).
///     </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class SlotConstraint : Resource
{
    /// <summary>
    ///     HARD pass/fail filter. Side-effect-free — called per-candidate-per-attempt by the
    ///     generator. Must return <c>true</c> (admit, the neutral value) rather than throw when its
    ///     required inputs are unavailable (e.g. metrics not yet computed in the pre-Sink spine
    ///     pass; see <see cref="RequiresMetrics" />).
    /// </summary>
    /// <remarks>
    ///     Internal because it consumes the internal mutable builder <see cref="PartialGraph" />;
    ///     polymorphic dispatch happens within the assembly (generator + rule subclasses).
    /// </remarks>
    internal abstract bool IsAdmissible(in Placement p, PartialGraph g);

    /// <summary>
    ///     True if this rule reads <see cref="IGraphMetrics" /> (critical edges, source-distances).
    ///     The generator filters metrics-required rules out of the pre-Sink spine pass, where the
    ///     metrics snapshot does not yet exist.
    /// </summary>
    public abstract bool RequiresMetrics { get; }
}
