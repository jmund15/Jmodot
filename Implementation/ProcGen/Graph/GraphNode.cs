namespace Jmodot.Implementation.ProcGen.Graph;

using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Immutable concrete <see cref="IGraphNode" />. Plain CLR (no Godot base) — constructed
///     by the generator at runtime, never .tres-authored.
/// </summary>
public sealed class GraphNode : IGraphNode
{
    public GraphNode(StringName id, INodeTemplate template)
    {
        this.Id = id;
        this.Template = template;
    }

    public StringName Id { get; }

    public INodeTemplate Template { get; }
}
