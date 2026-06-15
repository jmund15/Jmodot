namespace Jmodot.Implementation.ProcGen.Graph;

using System;
using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     SOFT placement bias toward port-rich templates: <c>weight = 1 + PerPortBonus·(ports − 1)</c>,
///     so a 4-port cross outweighs a 2-port corner. Added to a structure's
///     <see cref="SpineSpec.Weights" />, it keeps spine interiors port-rich enough to leave SPARE
///     ports for the alternate routes and branches that attach after the spine is laid — otherwise
///     a corner-heavy spine consumes both ports per interior node and the loops/branches silently
///     under-fill (gotcha_procgen_loop_embed_needs_turning_pieces). Stateless per the
///     <see cref="SlotWeight" /> contract (no mutable state beyond the <c>[Export]</c>).
/// </summary>
[GlobalClass, Tool]
public sealed partial class PortCountWeight : SlotWeight
{
    /// <summary>Extra multiplicative weight granted per port beyond the first. 0 = neutral; higher pulls harder toward tees/crosses.</summary>
    [Export(PropertyHint.Range, "0,16")]
    public int PerPortBonus { get; set; } = 3;

    /// <summary>Topological (port count) bias — reads no metrics, so it stays active in the pre-Sink spine pass.</summary>
    public override bool RequiresMetrics => false;

    internal override int Weight(in Placement p, PartialGraph g)
        => WeightForPortCount(p.Template.Ports.Count, this.PerPortBonus);

    /// <summary>
    ///     Pure scoring core: <c>max(1, 1 + perPortBonus·(ports − 1))</c>. Floored at 1 so a degenerate
    ///     input (0-port template, negative bonus) can never produce the zero/negative weight the
    ///     generator's <c>WeightedPick</c> rejects.
    /// </summary>
    internal static int WeightForPortCount(int ports, int perPortBonus)
        => Math.Max(1, 1 + (perPortBonus * (ports - 1)));
}
