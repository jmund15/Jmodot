namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Records which generation pass laid an edge and disambiguates sibling routes of the
///     same kind. <see cref="RouteOrdinal" /> separates shared-anchor rings: two loops pinned
///     to the same anchor share a <see cref="EdgeProvenanceKind.Loop" /> kind but carry
///     distinct ordinals, and every edge of one route shares that route's ordinal. Consumed
///     by the embedder's ordering input and the visualizer's graph view.
/// </summary>
/// <param name="Kind">Which pass laid the edge.</param>
/// <param name="RouteOrdinal">Per-route discriminator within a kind; <c>0</c> for the spine.</param>
public readonly record struct EdgeProvenance(EdgeProvenanceKind Kind, int RouteOrdinal);
