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
    ///     When true (default), the embed repair budget is auto-sized per floor from its node and
    ///     loop-edge count (the dynamic-budget knobs below), so a dense loopy floor gets a deeper
    ///     search and a sparse one doesn't waste compute. When false, <see cref="RepairBudget" /> is
    ///     used verbatim. Realizes the "tuned automatically" intent the flat static budget never met.
    /// </summary>
    [ExportGroup("Repair")]
    [Export]
    public bool DynamicBudget { get; set; } = true;

    /// <summary>
    ///     Bounded repair budget for the embed search: how many conflict-directed backjump repairs
    ///     the search may spend before returning a typed failure. Used verbatim when
    ///     <see cref="DynamicBudget" /> is off; the lower BOUND (floor) when it is on. Must be positive.
    /// </summary>
    [Export(PropertyHint.Range, "1,4096")]
    public int RepairBudget { get; set; } = 64;

    /// <summary>Dynamic-budget constant term — repairs granted to every floor regardless of size.</summary>
    [ExportSubgroup("Dynamic budget (auto-sized from topology)")]
    [Export(PropertyHint.Range, "0,4096")]
    public int BudgetBase { get; set; } = 256;

    /// <summary>Dynamic-budget repairs granted per graph node — placement count drives conflict count.</summary>
    [Export(PropertyHint.Range, "0,512")]
    public int BudgetPerNode { get; set; } = 48;

    /// <summary>
    ///     Dynamic-budget repairs granted per loop (cycle) edge — closure constraints are what the
    ///     conflict-directed backjumper actually spends budget resolving, so loops weigh heaviest.
    /// </summary>
    [Export(PropertyHint.Range, "0,1024")]
    public int BudgetPerLoopEdge { get; set; } = 128;

    /// <summary>Hard ceiling on the dynamic budget — bounds worst-case embed compute on a huge floor.</summary>
    [Export(PropertyHint.Range, "1,65536")]
    public int MaxBudget { get; set; } = 8192;
}
