namespace Jmodot.Core.ProcGen.Graph;

using Godot;

/// <summary>
///     A connection between two nodes' ports. Directionality is governed by
///     <see cref="Traversal" />; <see cref="IsGated" /> marks the edge as conditionally
///     passable. The kernel ships only the <c>bool</c> gate slot — gate <i>semantics</i>
///     (what unlocks it) are a downstream concern.
/// </summary>
public interface IGraphEdge
{
    /// <summary>Origin node.</summary>
    IGraphNode From { get; }

    /// <summary>Port on <see cref="From" /> this edge attaches to.</summary>
    StringName FromPort { get; }

    /// <summary>Destination node.</summary>
    IGraphNode To { get; }

    /// <summary>Port on <see cref="To" /> this edge attaches to.</summary>
    StringName ToPort { get; }

    /// <summary>True if traversal is conditionally blocked (e.g. behind a lock).</summary>
    bool IsGated { get; }

    /// <summary>Whether the edge is bidirectional or one-way From → To.</summary>
    EdgeTraversal Traversal { get; }
}
