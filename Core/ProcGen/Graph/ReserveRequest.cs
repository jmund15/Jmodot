namespace Jmodot.Core.ProcGen.Graph;

using Godot;

/// <summary>
///     A request to the <see cref="INodeRealizer" /> to reserve space for one node. A data-only
///     carrier — no behavior. <see cref="Near" /> is an optional geometry hint (place this node
///     adjacent to an already-reserved region); a geometry-free realizer may ignore it.
/// </summary>
public readonly struct ReserveRequest
{
    public ReserveRequest(INodeTemplate template, StringName nodeId, ReservedRegion? near)
    {
        this.Template = template;
        this.NodeId = nodeId;
        this.Near = near;
    }

    /// <summary>The template whose footprint is being reserved.</summary>
    public INodeTemplate Template { get; }

    /// <summary>Graph-unique id of the node this reservation is for.</summary>
    public StringName NodeId { get; }

    /// <summary>Optional adjacency hint (region to place near), or null for no hint.</summary>
    public ReservedRegion? Near { get; }
}
