namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;
using Jmodot.Core.Shared;

/// <summary>
///     The single global engine seam the floor-graph generator (P3a.6) reads: the shared procgen
///     context — template pool, global placement rules, node budget, pinned placements, and the
///     three isolated per-structure specs (<see cref="SpineSpec" />, <see cref="AlternateRouteSpec" />,
///     <see cref="BranchSpec" />). The load-bearing invariant: specs never cross-reference each
///     other — their only shared channel is this global config (the "draws-from-global" contract);
///     effective rules per placement = global ∪ structure-local.
///     <para>
///         Consumed polymorphically — the generator calls <see cref="Validate" /> at generation
///         start through this reference and never downcasts to the concrete profile (BOUNDARY
///         invariant). The PP-side <c>FloorSkeletonProfile</c> is the authored implementation.
///     </para>
/// </summary>
public interface ISkeletonConfig
{
    /// <summary>The pool of node templates the generator may place, null-filtered and viewed through the engine contract.</summary>
    IReadOnlyList<INodeTemplate> TemplatePool { get; }

    /// <summary>Global HARD placement filters applied to every candidate (null-filtered). Combined with each structure's locals: effective = global ∪ local.</summary>
    IReadOnlyList<SlotConstraint> GlobalConstraints { get; }

    /// <summary>Global SOFT placement biases applied to every survivor (null-filtered). Combined with each structure's locals.</summary>
    IReadOnlyList<SlotWeight> GlobalWeights { get; }

    /// <summary>Inclusive total node-count budget for the whole floor graph. Null leaves it to the generator default.</summary>
    IntRange? NodeBudget { get; }

    /// <summary>Forced placements the generator must honor (null-filtered).</summary>
    IReadOnlyList<PinnedPlacement> Pins { get; }

    /// <summary>The main-path spec, or null to leave spine shape to the generator default.</summary>
    SpineSpec? Spine { get; }

    /// <summary>The alternate-route spec, or null for none.</summary>
    AlternateRouteSpec? AlternateRoutes { get; }

    /// <summary>The branch spec, or null for none.</summary>
    BranchSpec? Branching { get; }

    /// <summary>
    ///     Fail-fast topology feasibility check, run at generation start. Throws
    ///     <see cref="System.ArgumentException" /> on a per-knob range violation (delegated to
    ///     <c>IntRange.Validate</c>) or <c>ResourceConfigurationException</c> on a cross-knob /
    ///     structural violation (over-subscribed budget, out-of-range pin, degenerate pool).
    /// </summary>
    void Validate();
}
