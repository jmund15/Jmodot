namespace Jmodot.Core.ProcGen.Spatial;

using System.Collections.Generic;
using Godot;

/// <summary>Published geometry for one loop edge closed by a synthesized connector.</summary>
public readonly record struct ConnectorRealization
{
    /// <summary>Graph node at the connector's first endpoint.</summary>
    public StringName FromNodeId { get; init; }

    /// <summary>Graph node at the connector's second endpoint.</summary>
    public StringName ToNodeId { get; init; }

    /// <summary>Port name at <see cref="FromNodeId" />.</summary>
    public StringName FromPort { get; init; }

    /// <summary>Port name at <see cref="ToNodeId" />.</summary>
    public StringName ToPort { get; init; }

    /// <summary>Axis-aligned connector boxes in normalized floor cells.</summary>
    public IReadOnlyList<CellPlacement> Boxes { get; init; }

    /// <summary>Creates a connector realization from its endpoint identity and boxes.</summary>
    public ConnectorRealization(
        StringName fromNodeId,
        StringName toNodeId,
        StringName fromPort,
        StringName toPort,
        IReadOnlyList<CellPlacement> boxes)
    {
        this.FromNodeId = fromNodeId;
        this.ToNodeId = toNodeId;
        this.FromPort = fromPort;
        this.ToPort = toPort;
        this.Boxes = boxes;
    }
}
