namespace Jmodot.Core.ProcGen.Graph;

using Jmodot.Implementation.ProcGen.Graph;

/// <summary>
///     Base for a SOFT endpoint bias: a stateless rule that scores how attractive an existing
///     <see cref="IGraphNode" /> is as an alternate-route endpoint in a given
///     <see cref="EndpointRole" /> (a divergence point X or a rejoin point Y). Distinct from
///     <see cref="SlotWeight" /> by payload — it scores a node-in-a-role, not a candidate
///     <see cref="Placement" /> of a template into a slot — so the two are separate families
///     (design decision B), not duplicates. The generator multiplies endpoint weights then does a
///     weighted pick over anchor pairs, so a weight must be <c>&gt;= 1</c> (1 = neutral / no bias).
///     <para>
///         Mirrors the <see cref="SlotWeight" /> precedent: a <c>[GlobalClass,Tool]</c> Resource
///         with <c>[Export]</c> tuning fields on each subclass and a pure evaluation method.
///         <b>Stateless by contract</b> — no mutable instance fields beyond <c>[Export]</c>s
///         (see arch_rule_transition_condition_stateless).
///     </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class EndpointWeight : Resource
{
    /// <summary>
    ///     SOFT multiplicative bias, <c>&gt;= 1</c>. Side-effect-free — called per-candidate-endpoint
    ///     by the generator. Must return <c>1</c> (neutral) rather than throw when its required inputs
    ///     are unavailable (e.g. metrics not yet computed; see <see cref="RequiresMetrics" />) or when
    ///     the node is outside this rule's concern.
    /// </summary>
    /// <remarks>
    ///     Internal because it consumes the internal mutable builder <see cref="PartialGraph" />;
    ///     polymorphic dispatch happens within the assembly (generator + rule subclasses).
    /// </remarks>
    internal abstract int Weight(IGraphNode node, EndpointRole role, PartialGraph g);

    /// <summary>
    ///     True if this rule reads <see cref="IGraphMetrics" /> (critical edges, source-distances).
    ///     The generator filters metrics-required rules out of passes where the metrics snapshot does
    ///     not yet exist.
    /// </summary>
    public abstract bool RequiresMetrics { get; }
}
