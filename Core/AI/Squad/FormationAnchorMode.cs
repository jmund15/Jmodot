namespace Jmodot.Core.AI.Squad;

/// <summary>
/// Defines how the formation's anchor point is determined.
/// </summary>
public enum FormationAnchorMode
{
    /// <summary>
    /// Formation is anchored relative to the leader (slot 0).
    /// Leader position defines the formation origin.
    /// </summary>
    Leader,

    /// <summary>
    /// Formation is anchored at the centroid of all member positions.
    /// Useful for keeping formation centered on the group.
    /// </summary>
    Centroid,

    /// <summary>
    /// Formation uses a fixed world position as anchor.
    /// Useful for "hold position" or "rally point" behaviors.
    /// </summary>
    Static
}
