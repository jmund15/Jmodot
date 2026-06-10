namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;
using Godot;

/// <summary>
///     The reusable "kind" of a node — its identity and the set of ports it exposes.
///     Many <see cref="IGraphNode" /> instances may share one template. Carries no
///     game semantics and no coordinates.
/// </summary>
public interface INodeTemplate
{
    /// <summary>Identifier of this template (e.g. "combat_room", "junction").</summary>
    StringName TemplateId { get; }

    /// <summary>The connection points this template exposes.</summary>
    IReadOnlyList<IGraphPort> Ports { get; }

    /// <summary>
    ///     The structural role this template plays during routing (<see cref="TemplateRole.Body" />
    ///     vs <see cref="TemplateRole.Connector" />). Engine-readable so the generator can order
    ///     routing candidates by role without downcasting to a concrete template type (boundary rule).
    /// </summary>
    TemplateRole Role { get; }
}
