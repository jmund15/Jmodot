namespace Jmodot.Core.ProcGen.Graph;

using Godot;

/// <summary>
///     A vertex in the floor graph: a unique id plus the template defining its ports.
///     Identity is the <see cref="Id" /> — metrics and adjacency are keyed by it.
/// </summary>
public interface IGraphNode
{
    /// <summary>Graph-unique identifier of this node.</summary>
    StringName Id { get; }

    /// <summary>The template defining this node's kind and ports.</summary>
    INodeTemplate Template { get; }
}
