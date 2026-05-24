namespace Jmodot.Core.ProcGen.Graph;

using Godot;

/// <summary>
///     A named connection point on a node template. Edges attach to a node via a port,
///     letting a template expose typed slots (e.g. "entrance", "exit") without the kernel
///     understanding their game meaning. Coordinate-free — spatial realization is P3's concern.
/// </summary>
public interface IGraphPort
{
    /// <summary>Identifier of this port, unique within its owning template.</summary>
    StringName Name { get; }

    /// <summary>Opaque category tag (e.g. door-kind) the generator/consumers interpret.</summary>
    StringName Type { get; }
}
