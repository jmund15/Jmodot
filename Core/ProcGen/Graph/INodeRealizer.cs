namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;

/// <summary>
///     The geometry seam the floor-graph generator talks through (boundary inversion, like
///     <see cref="ISkeletonConfig" />). The generator asks "can this node's footprint be placed?"
///     without knowing how geometry works; a real realizer (P3b) answers with grid packing, a
///     geometry-free mock answers deterministically so the generator's whole TDD cycle stays
///     pure-Logic.
///     <para>
///     <b>Stateless by contract</b> — there is no <c>Reset</c>; <paramref name="occupied" /> is the
///     only state channel, supplied by the caller each call. An implementation that needs to veto a
///     placement returns <c>false</c> (the caller retries elsewhere).
///     </para>
/// </summary>
public interface INodeRealizer
{
    /// <summary>
    ///     Attempts to reserve space for <paramref name="request" /> given the currently
    ///     <paramref name="occupied" /> regions. Returns <c>true</c> with the reserved
    ///     <paramref name="region" /> on success, or <c>false</c> (region defaulted) if vetoed.
    /// </summary>
    bool TryReserve(in ReserveRequest request, IReadOnlyCollection<ReservedRegion> occupied, out ReservedRegion region);
}
