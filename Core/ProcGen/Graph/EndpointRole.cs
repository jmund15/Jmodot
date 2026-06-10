namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     The role a graph node plays at the end of an alternate route. A plain CLR enum (never
///     .tres-authored) so the graph kernel stays pure-CLR and dodges the Godot type-load
///     allocation class.
/// </summary>
public enum EndpointRole
{
    /// <summary>The route leaves the backbone here — the X endpoint.</summary>
    Divergence = 0,

    /// <summary>The route rejoins the backbone here — the Y endpoint.</summary>
    Rejoin,
}
