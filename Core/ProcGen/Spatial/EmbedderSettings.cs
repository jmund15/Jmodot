namespace Jmodot.Core.ProcGen.Spatial;

using Godot;

/// <summary>
///     Authoring knobs for the stage-2 embedder (design-se §4). A plain data carrier — the
///     embedder validates at <c>Embed</c> entry (a non-positive budget throws there), so authored
///     <c>.tres</c> values stay inspectable rather than self-correcting.
/// </summary>
[GlobalClass, Tool]
public partial class EmbedderSettings : Resource
{
    /// <summary>
    ///     Bounded repair budget for the embed search: how many conflict-directed backjump repairs
    ///     the search may spend before returning a typed failure. Must be positive; tuned via the
    ///     seed-corpus gate.
    /// </summary>
    [Export(PropertyHint.Range, "1,1024")]
    public int RepairBudget { get; set; } = 64;
}
