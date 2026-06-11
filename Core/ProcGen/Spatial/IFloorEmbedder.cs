namespace Jmodot.Core.ProcGen.Spatial;

using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Stage-2 seam of the two-stage floor pipeline (design-se §1/§4): consumes a finished, immutable
///     stage-1 topology and produces integer-grid cell placements + doorway poses. Implementations
///     are RNG-FREE deterministic searches — identical topology + config must yield an identical
///     embedding; randomness, if ever wanted, enters as injected rolls, never embedder-local helpers.
/// </summary>
public interface IFloorEmbedder
{
    FloorEmbedResult Embed(IFloorGraph topology, GeometryEnvelope envelope, EmbedderSettings settings);
}
