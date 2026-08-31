namespace Jmodot.Core.ProcGen.Graph;

/// <summary>Controls whether loop edges may use synthesized connector corridors.</summary>
public enum ConnectorPolicy
{
    /// <summary>Permit a loop edge to close with a collision-free synthesized corridor.</summary>
    Closable = 0,

    /// <summary>Require loop edges to close by direct room abutment.</summary>
    AbutmentOnly = 1,

    /// <summary>
    ///     Everything <see cref="Closable" /> permits, PLUS the BACK-TO-BACK wrap-around family — corridors
    ///     that reach behind a room to join two ports facing directly AWAY from each other. It is only that
    ///     family this adds: the wrap joining two ports facing the SAME direction is already part of
    ///     <see cref="Closable" /> and needs no opt-in. Closes more loops, at the cost
    ///     of committing the layout to a long ring corridor that can starve later placements on a tight
    ///     envelope — measured to cost loop-closure reliability on the debug floor profile. Opt in when
    ///     the envelope has room to spare and loop density matters more than placement headroom.
    /// </summary>
    ClosableWraparound = 2,
}
