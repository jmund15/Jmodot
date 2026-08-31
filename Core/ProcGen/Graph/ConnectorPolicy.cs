namespace Jmodot.Core.ProcGen.Graph;

/// <summary>Controls whether loop edges may use synthesized connector corridors.</summary>
public enum ConnectorPolicy
{
    /// <summary>Permit a loop edge to close with a collision-free synthesized corridor.</summary>
    Closable = 0,

    /// <summary>Require loop edges to close by direct room abutment.</summary>
    AbutmentOnly = 1,
}
