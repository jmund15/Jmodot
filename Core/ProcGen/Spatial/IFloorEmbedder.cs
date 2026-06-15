namespace Jmodot.Core.ProcGen.Spatial;

using Jmodot.Core.ProcGen;
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

    /// <summary>
    ///     Opens a PROGRESSIVE embedding session over a committed backbone (its spine is embedded +
    ///     frozen immediately): the generator validates decorations against the real grid before
    ///     committing them, then emits the final layout reusing every frozen pose. The seam that lets
    ///     the generator avoid the re-roll-the-whole-floor cost of a stage-2 embed miss.
    /// </summary>
    ILayoutAdvisor BeginSession(IFloorGraph backbone, GeometryEnvelope envelope, EmbedderSettings settings);
}
