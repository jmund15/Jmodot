namespace Jmodot.Core.ProcGen.Spatial;

using Godot;

/// <summary>
///     The post-bake integer footprint of a node template, in cells. The stage-2 embedder reads this
///     off a template without downcasting to a concrete PP type (framework boundary) — mirroring how
///     <see cref="Jmodot.Core.ProcGen.Graph.INodeTemplate" /> stays coordinate-free for the kernel.
/// </summary>
public interface ISpatialNodeTemplate
{
    /// <summary>Post-bake integer footprint in cells (X, Y, Z). Any dimension &lt;= 0 means unbaked and is rejected by the owning template's validation.</summary>
    Vector3I FootprintCells { get; }

    /// <summary>Whether a connector corridor may terminate at <paramref name="port" />. The default is false (fail closed); an implementation opts in per its own wall-geometry semantics — a port with verified or absent wall geometry may realize, an unverified one may not.</summary>
    bool CanRealizeConnectorAt(ISpatialPort port) => false;
}
