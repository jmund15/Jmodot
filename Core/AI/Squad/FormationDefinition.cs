namespace Jmodot.Core.AI.Squad;

using Godot;

/// <summary>
/// Data-driven definition of a squad formation.
/// SlotOffsets define relative positions in local space where:
/// - +X is right
/// - +Z is forward (toward the direction the formation faces)
/// - Slot 0 is conventionally the leader position
/// </summary>
[GlobalClass]
public partial class FormationDefinition : Resource
{
    /// <summary>
    /// Relative positions for each slot in local formation space.
    /// Slot 0 is conventionally the leader. When AnchorMode is Leader,
    /// slot 0's offset should typically be Vector3.Zero.
    /// </summary>
    [Export]
    public Vector3[] SlotOffsets { get; set; } = System.Array.Empty<Vector3>();

    /// <summary>
    /// Minimum spacing between members in meters.
    /// Used by slot assignment strategies to ensure comfortable spacing.
    /// Designer-intuitive: 1.5 = 1.5 meters between members.
    /// </summary>
    [Export]
    public float MinSpacing { get; set; } = 1.5f;

    /// <summary>
    /// Human-readable name for this formation (e.g., "Line", "V-Formation", "Circle").
    /// </summary>
    [Export]
    public string FormationName { get; set; } = "Unnamed";

    /// <summary>
    /// Number of slots in this formation.
    /// </summary>
    public int SlotCount => SlotOffsets?.Length ?? 0;
}
