namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Which generation pass laid an edge. Three valid kinds — <see cref="Spine" /> (the
///     backbone path), <see cref="Loop" /> (an alternate route ring), <see cref="Branch" />
///     (a dead-end growth tree). A plain CLR enum (never .tres-authored) so the graph kernel
///     stays pure-CLR and dodges the Godot type-load allocation class.
///     <para>
///         <see cref="Unset" /> is invalid-at-zero: representable transiently while an edge is
///         under construction, but rejected at <c>FloorGraph</c> build and
///         <c>GraphSignature.Of</c>. A forgotten stamp therefore fails loudly at the gate
///         rather than masquerading as a valid kind.
///     </para>
/// </summary>
public enum EdgeProvenanceKind
{
    /// <summary>No pass stamped this edge — invalid past construction; rejected at build/signature.</summary>
    Unset = 0,

    /// <summary>Laid by the spine pass (the backbone path).</summary>
    Spine,

    /// <summary>Laid by a loop pass (an alternate route ring).</summary>
    Loop,

    /// <summary>Laid by a branch pass (a dead-end growth tree).</summary>
    Branch,

    /// <summary>
    ///     Laid by a hand-authored floor definition rather than a generation pass — the generic,
    ///     boundary-clean provenance for adapting an authored topology into an <c>IFloorGraph</c>.
    ///     Passes the <c>FloorGraph</c> build gate, which rejects only <see cref="Unset" />.
    /// </summary>
    Authored,

    /// <summary>
    ///     Laid by the pinned-neighbor pass: a REQUIRED room a <see cref="PinnedPlacement" />
    ///     demands directly adjacent to its pinned node (set-piece flanks). Appended last to
    ///     preserve the append-only ordinal contract.
    /// </summary>
    PinnedNeighbor,
}
