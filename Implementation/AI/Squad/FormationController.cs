namespace Jmodot.Implementation.AI.Squad;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.AI.Squad;

/// <summary>
/// Static utility class for calculating formation slot world positions.
/// Pure functions - no state, fully testable.
/// </summary>
public static class FormationController
{
    private const float Epsilon = 0.0001f;

    /// <summary>
    /// Calculates world positions for each formation slot.
    /// </summary>
    /// <param name="formation">The formation definition with slot offsets.</param>
    /// <param name="anchorMode">How to determine the formation anchor point.</param>
    /// <param name="anchorPosition">World position for Leader/Static modes.</param>
    /// <param name="anchorForward">The direction the formation faces (normalized).</param>
    /// <param name="memberPositions">Current member positions (required for Centroid mode).</param>
    /// <returns>Dictionary mapping slot index to world position.</returns>
    public static Dictionary<int, Vector3> CalculateSlotPositions(
        FormationDefinition formation,
        FormationAnchorMode anchorMode,
        Vector3 anchorPosition,
        Vector3 anchorForward,
        IReadOnlyList<Vector3>? memberPositions = null)
    {
        var result = new Dictionary<int, Vector3>();

        if (formation.SlotOffsets == null || formation.SlotOffsets.Length == 0)
        {
            return result;
        }

        // 1. Determine the anchor point based on mode
        Vector3 worldAnchor = DetermineAnchor(anchorMode, anchorPosition, memberPositions);

        // 2. Calculate the rotation basis to transform local offsets to world space
        // Local formation space: -Z is forward (matches Godot convention)
        // We need to rotate local space so that local -Z aligns with anchorForward
        Basis rotationBasis = CalculateRotationBasis(anchorForward);

        // 3. Transform each slot offset to world position
        for (int i = 0; i < formation.SlotOffsets.Length; i++)
        {
            Vector3 localOffset = formation.SlotOffsets[i];
            Vector3 worldOffset = rotationBasis * localOffset;
            result[i] = worldAnchor + worldOffset;
        }

        return result;
    }

    /// <summary>
    /// Determines the anchor point based on the anchor mode.
    /// </summary>
    private static Vector3 DetermineAnchor(
        FormationAnchorMode anchorMode,
        Vector3 anchorPosition,
        IReadOnlyList<Vector3>? memberPositions)
    {
        if (anchorMode == FormationAnchorMode.Centroid &&
            memberPositions != null &&
            memberPositions.Count > 0)
        {
            return CalculateCentroid(memberPositions);
        }

        // Leader and Static modes both use the provided anchor position
        // Centroid mode falls back to anchor if no members provided
        return anchorPosition;
    }

    /// <summary>
    /// Calculates the centroid (average position) of the provided positions.
    /// </summary>
    private static Vector3 CalculateCentroid(IReadOnlyList<Vector3> positions)
    {
        Vector3 sum = Vector3.Zero;
        foreach (var pos in positions)
        {
            sum += pos;
        }
        return sum / positions.Count;
    }

    /// <summary>
    /// Calculates a rotation basis that transforms local formation space to world space.
    /// Local -Z (forward) will align with the provided world forward direction.
    /// </summary>
    private static Basis CalculateRotationBasis(Vector3 worldForward)
    {
        // Normalize the forward direction
        Vector3 forward = worldForward.LengthSquared() > Epsilon
            ? worldForward.Normalized()
            : Vector3.Forward;

        // Use Basis.LookingAt to create a basis where -Z points toward worldForward
        // LookingAt creates a basis that looks "at" the target from origin,
        // meaning the -Z axis of the resulting basis points toward the target.
        // Since our local -Z should align with worldForward, this is exactly what we need.
        return Basis.LookingAt(forward, Vector3.Up);
    }
}
