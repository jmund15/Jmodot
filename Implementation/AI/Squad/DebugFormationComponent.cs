namespace Jmodot.Implementation.AI.Squad;

using System.Collections.Generic;
using Godot;
using BB;
using Core.AI.BB;
using UtilityAI;
using Shared;

/// <summary>
/// Debug visualization component for squad formations.
/// Attach as a child of SquadManager to visualize slot positions and member assignments.
/// Uses DebugDraw3D for runtime visualization.
/// </summary>
[GlobalClass]
public partial class DebugFormationComponent : Node
{
    #region Exported Parameters

    [ExportGroup("Visualization")]

    /// <summary>
    /// Master toggle for debug visualization.
    /// </summary>
    [Export]
    private bool _enableDebug = true;

    /// <summary>
    /// Color for empty (unoccupied) slot positions.
    /// </summary>
    [Export]
    private Color _emptySlotColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    /// <summary>
    /// Color for occupied slot positions.
    /// </summary>
    [Export]
    private Color _occupiedSlotColor = new Color(0.2f, 0.8f, 0.2f, 0.7f);

    /// <summary>
    /// Color for the leader slot (slot 0).
    /// </summary>
    [Export]
    private Color _leaderSlotColor = new Color(1.0f, 0.8f, 0.0f, 0.7f);

    /// <summary>
    /// Color for the line connecting members to their slots.
    /// </summary>
    [Export]
    private Color _connectionLineColor = new Color(0.3f, 0.6f, 1.0f, 0.5f);

    /// <summary>
    /// Radius of the debug spheres drawn at slot positions.
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 2.0, 0.1")]
    private float _slotSphereRadius = 0.5f;

    /// <summary>
    /// Height offset for visualization (to avoid z-fighting with ground).
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 2.0, 0.1")]
    private float _heightOffset = 0.1f;

    [ExportGroup("References")]

    /// <summary>
    /// Optional direct reference to the SquadManager.
    /// If not set, will try to find parent SquadManager.
    /// </summary>
    [Export]
    private SquadManager? _squadManager;

    #endregion

    private IBlackboard? _squadBlackboard;

    public override void _Ready()
    {
        // Try to find SquadManager if not explicitly set
        if (_squadManager == null)
        {
            _squadManager = GetParentOrNull<SquadManager>();
        }

        if (_squadManager == null)
        {
            JmoLogger.Warning(this, "DebugFormationComponent: No SquadManager found. Attach as child of SquadManager or set reference manually.");
            return;
        }

        // Try to get the squad blackboard
        _squadBlackboard = _squadManager.GetFirstChildOfInterface<IBlackboard>();
    }

    public override void _Process(double delta)
    {
        if (!_enableDebug || _squadBlackboard == null)
        {
            return;
        }

        DrawFormationSlots();
    }

    /// <summary>
    /// Draws the formation slot positions and connections to members.
    /// </summary>
    private void DrawFormationSlots()
    {
        // Check if formation is active
        if (!_squadBlackboard!.TryGet<bool>(BBDataSig.FormationActive, out var isActive) || !isActive)
        {
            return;
        }

        // Get slot positions
        if (!_squadBlackboard.TryGet<Dictionary<int, Vector3>>(BBDataSig.FormationSlotPositions, out var slotPositions) ||
            slotPositions == null)
        {
            return;
        }

        // Get leader reference for special coloring
        _squadBlackboard.TryGet<Node3D>(BBDataSig.FormationLeader, out var leader);

        // Track which slots are occupied
        var occupiedSlots = new HashSet<int>();

        // Draw connections from members to their slots
        if (_squadManager != null)
        {
            DrawMemberConnections(slotPositions, occupiedSlots);
        }

        // Draw slot positions
        foreach (var kvp in slotPositions)
        {
            int slotIndex = kvp.Key;
            Vector3 slotPos = kvp.Value;
            slotPos.Y += _heightOffset;

            // Determine color based on slot state
            Color slotColor;
            if (slotIndex == 0)
            {
                slotColor = _leaderSlotColor;
            }
            else if (occupiedSlots.Contains(slotIndex))
            {
                slotColor = _occupiedSlotColor;
            }
            else
            {
                slotColor = _emptySlotColor;
            }

            // Draw sphere at slot position
            DebugDraw3D.DrawSphere(slotPos, _slotSphereRadius, slotColor);

            // Draw slot index as text above the sphere
            DebugDraw3D.DrawText(slotPos + Vector3.Up * _slotSphereRadius * 2f, $"[{slotIndex}]", 16, slotColor);
        }
    }

    /// <summary>
    /// Draws lines from each squad member to their assigned slot.
    /// </summary>
    private void DrawMemberConnections(Dictionary<int, Vector3> slotPositions, HashSet<int> occupiedSlots)
    {
        // Access members through reflection or exposed property if available
        // For now, iterate through children of squad manager looking for agents with blackboards
        foreach (var child in GetTree().GetNodesInGroup("SquadMembers"))
        {
            if (child is not Node3D member3D)
            {
                continue;
            }

            // Try to get the member's blackboard
            var memberBB = member3D.GetFirstChildOfInterface<IBlackboard>();
            if (memberBB == null)
            {
                continue;
            }

            // Get their assigned slot
            if (!memberBB.TryGet<int>(BBDataSig.FormationSlotIndex, out var slotIndex) || slotIndex < 0)
            {
                continue;
            }

            // Mark slot as occupied
            occupiedSlots.Add(slotIndex);

            // Get slot position
            if (!slotPositions.TryGetValue(slotIndex, out var slotPos))
            {
                continue;
            }

            // Draw connection line
            Vector3 memberPos = member3D.GlobalPosition;
            memberPos.Y += _heightOffset;
            slotPos.Y += _heightOffset;

            DebugDraw3D.DrawLine(memberPos, slotPos, _connectionLineColor);
        }
    }

    /// <summary>
    /// Manually sets the squad blackboard for testing or direct wiring.
    /// </summary>
    public void SetSquadBlackboard(IBlackboard blackboard)
    {
        _squadBlackboard = blackboard;
    }
}
