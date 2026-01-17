namespace Jmodot.Core.AI.Squad;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Strategy interface for assigning squad members to formation slots.
/// Different strategies can optimize for different goals:
/// - NearestSlot: Minimize total travel distance (greedy)
/// - Hungarian: Optimal assignment (more expensive)
/// - RoleBasedSlot: Assign by member role/type
/// </summary>
public interface ISlotAssignmentStrategy
{
    /// <summary>
    /// Assigns members to formation slots.
    /// </summary>
    /// <param name="memberPositions">Current world positions of squad members (index = member ID).</param>
    /// <param name="slotPositions">Available slot world positions (key = slot index).</param>
    /// <param name="leaderMemberIndex">Optional: If specified, this member MUST be assigned to slot 0.</param>
    /// <returns>Dictionary mapping member index to slot index. -1 means unassigned.</returns>
    Dictionary<int, int> AssignSlots(
        IReadOnlyList<Vector3> memberPositions,
        IReadOnlyDictionary<int, Vector3> slotPositions,
        int leaderMemberIndex = -1);
}
