namespace Jmodot.Core.ProcGen;

using Godot;
using Jmodot.Core.ProcGen.Spatial;
using Jmodot.Core.Shared.Attributes;

/// <summary>
///     Pipeline-tier knobs for <c>FloorPipeline.Generate</c> (design-se §1): the topology re-roll
///     budget plus the stage-2 embedder settings. Lives at the ProcGen root because the pipeline
///     spans both stages — stage-scoped knobs belong on <see cref="EmbedderSettings" /> or the
///     skeleton config, not here.
/// </summary>
[GlobalClass, Tool]
public partial class FloorPipelineSettings : Resource
{
    /// <summary>
    ///     Topology re-roll budget: how many derived floor seeds the pipeline tries before giving
    ///     up. ≤ 0 is rejected at Generate entry (a resaved .tres can null-strip this to 0; the
    ///     guard turns that silent zero into a loud configuration error).
    /// </summary>
    [ExportGroup("Advanced — leave at defaults")]
    [Export(PropertyHint.Range, "1,100")]
    public int MaxFloorAttempts { get; set; } = 16;

    /// <summary>Stage-2 embedder settings; required — a stripped slot fails loud at Generate entry.</summary>
    [Export, RequiredExport]
    public EmbedderSettings Embedder { get; set; } = null!;

    /// <summary>
    ///     When true, the pipeline builds the floor through the embedder's PROGRESSIVE session: the
    ///     generator validates each loop/branch against the real grid as it lays them (only embeddable
    ///     decorations are committed), then emits the final layout reusing the frozen poses — converting
    ///     a stage-2 embed miss (the dominant re-roll cause) into a cheap in-generator rejection. False
    ///     (default) keeps the classic generate-then-embed path. Requires an embedder that supports
    ///     <see cref="IFloorEmbedder.BeginSession" /> (the built-in grid embedder does).
    /// </summary>
    [Export]
    public bool UseProgressiveEmbed { get; set; }
}
