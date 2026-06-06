namespace Jmodot.Implementation.ProcGen.Graph;

using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     A <see cref="CandidateSlot" /> for an open (unconnected) port on a node — the generator may
///     attach a new node here. Id is <c>port{Sep}{Node.Id}{Sep}{Port.Name}</c>.
/// </summary>
public sealed class PortSlot : CandidateSlot
{
    public PortSlot(IGraphNode node, IGraphPort port)
        : base(new StringName($"port{Sep}{node.Id}{Sep}{port.Name}"))
    {
        this.Node = node;
        this.Port = port;
    }

    public IGraphNode Node { get; }

    public IGraphPort Port { get; }

    public override IGraphNode AnchorNode => this.Node;
}
