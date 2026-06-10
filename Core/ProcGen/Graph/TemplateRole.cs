namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     The structural role a node template plays during routing. A plain CLR enum (never
///     .tres-authored) so the graph kernel stays pure-CLR. Engine-readable on
///     <see cref="INodeTemplate" /> so the generator can order routing candidates by role
///     without downcasting to a concrete template type (the boundary rule forbids the engine
///     from naming a consumer's template type).
/// </summary>
public enum TemplateRole
{
    /// <summary>A primary room template — the default.</summary>
    Body = 0,

    /// <summary>A bridging/corridor template preferred for connecting bodies along a route.</summary>
    Connector,
}
