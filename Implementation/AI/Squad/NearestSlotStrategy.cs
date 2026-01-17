namespace Jmodot.Implementation.AI.Squad;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.AI.Squad;

/// <summary>
/// Greedy nearest-first slot assignment strategy.
/// Complexity: O(n²) where n = max(members, slots).
/// Good for small-medium squads (6-10 members).
/// For larger squads, consider Hungarian algorithm implementation.
/// </summary>
public class NearestSlotStrategy : ISlotAssignmentStrategy
{
    /// <inheritdoc />
    public Dictionary<int, int> AssignSlots(
        IReadOnlyList<Vector3> memberPositions,
        IReadOnlyDictionary<int, Vector3> slotPositions,
        int leaderMemberIndex = -1)
    {
        var assignments = new Dictionary<int, int>();

        if (memberPositions.Count == 0)
        {
            return assignments;
        }

        // Track which slots have been claimed
        var availableSlots = new HashSet<int>(slotPositions.Keys);

        // If no slots available, all members are unassigned
        if (availableSlots.Count == 0)
        {
            for (int i = 0; i < memberPositions.Count; i++)
            {
                assignments[i] = -1;
            }
            return assignments;
        }

        // Track which members still need assignment
        var unassignedMembers = new HashSet<int>(Enumerable.Range(0, memberPositions.Count));

        // Step 1: If leader is specified, assign them to slot 0 first
        if (leaderMemberIndex >= 0 &&
            leaderMemberIndex < memberPositions.Count &&
            availableSlots.Contains(0))
        {
            assignments[leaderMemberIndex] = 0;
            availableSlots.Remove(0);
            unassignedMembers.Remove(leaderMemberIndex);
        }

        // Step 2: Greedy assignment for remaining members
        // Process members in order, each picks their nearest available slot
        foreach (int memberIndex in unassignedMembers.ToList()) // ToList to avoid modification during iteration
        {
            if (availableSlots.Count == 0)
            {
                // No more slots available
                assignments[memberIndex] = -1;
                continue;
            }

            Vector3 memberPos = memberPositions[memberIndex];
            int nearestSlot = FindNearestSlot(memberPos, availableSlots, slotPositions);

            assignments[memberIndex] = nearestSlot;
            availableSlots.Remove(nearestSlot);
        }

        return assignments;
    }

    /// <summary>
    /// Finds the nearest available slot to a given position.
    /// </summary>
    private static int FindNearestSlot(
        Vector3 position,
        HashSet<int> availableSlots,
        IReadOnlyDictionary<int, Vector3> slotPositions)
    {
        int nearestSlot = -1;
        float nearestDistSq = float.MaxValue;

        foreach (int slotIndex in availableSlots)
        {
            float distSq = position.DistanceSquaredTo(slotPositions[slotIndex]);
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearestSlot = slotIndex;
            }
        }

        return nearestSlot;
    }
}
