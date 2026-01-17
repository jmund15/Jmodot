// --- SquadManager.cs ---
namespace Jmodot.Implementation.AI.UtilityAI;

using System.Collections.Generic;
using System.Linq;
using Godot;
using BB;
using Core.AI.BB;
using Core.AI.Squad;
using Shared;
using Squad;

/// <summary>
/// Manages a squad of AI agents, updating shared state on a squad-level blackboard.
/// Individual agents can read squad tags to coordinate behavior via HasTagConsideration.
/// Enhanced with formation support for coordinated movement.
/// </summary>
[GlobalClass]
public partial class SquadManager : Node
{
    #region Squad Tags

    [ExportGroup("Squad Tags")]
    [Export]
    private StringName _highThreatTag = "Squad_Panic";

    [Export]
    private StringName _regroupTag = "Squad_Regroup";

    [Export]
    private StringName _attackTag = "Squad_Attack";

    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _panicHealthThreshold = 0.25f;

    #endregion

    #region Formation Configuration

    [ExportGroup("Formation")]
    [Export]
    private FormationDefinition? _defaultFormation;

    [Export]
    private FormationAnchorMode _anchorMode = FormationAnchorMode.Leader;

    #endregion

    private Blackboard? _squadBlackboard;
    private List<IBlackboard> _memberBlackboards = new();
    private List<Node3D> _memberNodes = new();

    // Formation state
    private FormationDefinition? _currentFormation;
    private FormationAnchorMode _currentAnchorMode;
    private ISlotAssignmentStrategy _slotStrategy = new NearestSlotStrategy();
    private int _leaderMemberIndex = -1;

    public override void _Ready()
    {
        // Try to find blackboard child - may be pre-added or added later
        _squadBlackboard = this.GetFirstChildOfType<Blackboard>();

        // Initialize with default formation if set
        if (_defaultFormation != null)
        {
            SetFormation(_defaultFormation, _anchorMode);
        }
    }

    /// <summary>
    /// For testing: Manually inject the squad blackboard.
    /// </summary>
    internal void SetSquadBlackboard(Blackboard blackboard)
    {
        _squadBlackboard = blackboard;
    }

    #region Member Management

    /// <summary>
    /// Adds a member to the squad and assigns them a formation slot.
    /// </summary>
    /// <param name="memberNode">The Node3D representing the squad member.</param>
    /// <param name="isLeader">If true, this member becomes the formation leader (slot 0).</param>
    public void AddMember(Node3D memberNode, bool isLeader = false)
    {
        var bb = memberNode.GetFirstChildOfInterface<IBlackboard>();
        if (bb == null)
        {
            JmoLogger.Warning(this, $"Cannot add member {memberNode.Name}: no IBlackboard found");
            return;
        }

        AddMemberInternal(memberNode, bb, isLeader);
    }

    /// <summary>
    /// Adds a member with an explicitly provided blackboard.
    /// Useful for testing or when blackboard is not a direct child.
    /// </summary>
    public void AddMember(Node3D memberNode, IBlackboard memberBlackboard, bool isLeader = false)
    {
        AddMemberInternal(memberNode, memberBlackboard, isLeader);
    }

    private void AddMemberInternal(Node3D memberNode, IBlackboard bb, bool isLeader)
    {
        // Add to tracking lists
        _memberNodes.Add(memberNode);
        _memberBlackboards.Add(bb);
        bb.SetParent(_squadBlackboard);

        // Handle leader designation
        if (isLeader)
        {
            _leaderMemberIndex = _memberNodes.Count - 1;
            _squadBlackboard?.Set(BBDataSig.FormationLeader, memberNode);
        }

        // Reassign all slots when membership changes
        ReassignSlots();
    }

    /// <summary>
    /// Removes a member from the squad.
    /// </summary>
    public void RemoveMember(Node3D memberNode)
    {
        int index = _memberNodes.IndexOf(memberNode);
        if (index < 0)
        {
            return;
        }

        var bb = _memberBlackboards[index];
        bb.SetParent(null);
        bb.Set(BBDataSig.FormationSlotIndex, -1);

        _memberNodes.RemoveAt(index);
        _memberBlackboards.RemoveAt(index);

        // Update leader index if needed
        if (_leaderMemberIndex == index)
        {
            _leaderMemberIndex = -1;
            _squadBlackboard?.Set<Node3D?>(BBDataSig.FormationLeader, null);
        }
        else if (_leaderMemberIndex > index)
        {
            _leaderMemberIndex--;
        }

        ReassignSlots();
    }

    #endregion

    #region Formation Management

    /// <summary>
    /// Sets the current formation and triggers slot reassignment.
    /// </summary>
    /// <param name="formation">The formation definition to use.</param>
    /// <param name="anchorMode">How to anchor the formation in world space.</param>
    public void SetFormation(FormationDefinition formation, FormationAnchorMode anchorMode)
    {
        _currentFormation = formation;
        _currentAnchorMode = anchorMode;

        _squadBlackboard?.Set(BBDataSig.FormationActive, true);

        // Reassign slots for all existing members
        ReassignSlots();
    }

    /// <summary>
    /// Clears the current formation.
    /// </summary>
    public void ClearFormation()
    {
        _currentFormation = null;
        _squadBlackboard?.Set(BBDataSig.FormationActive, false);
        _squadBlackboard?.Set<Dictionary<int, Vector3>?>(BBDataSig.FormationSlotPositions, null);

        // Clear slot assignments from all members
        foreach (var bb in _memberBlackboards)
        {
            bb.Set(BBDataSig.FormationSlotIndex, -1);
        }
    }

    /// <summary>
    /// Updates formation slot world positions based on anchor and forward direction.
    /// Call this when the squad moves or the anchor changes.
    /// </summary>
    /// <param name="anchorPosition">World position for the formation anchor.</param>
    /// <param name="anchorForward">Direction the formation faces.</param>
    public void UpdateFormationPositions(Vector3 anchorPosition, Vector3 anchorForward)
    {
        if (_currentFormation == null || _squadBlackboard == null)
        {
            return;
        }

        // Get member positions for centroid calculation if needed
        List<Vector3>? memberPositions = null;
        if (_currentAnchorMode == FormationAnchorMode.Centroid)
        {
            memberPositions = _memberNodes
                .Where(n => IsInstanceValid(n))
                .Select(n => n.GlobalPosition)
                .ToList();
        }

        var slotPositions = FormationController.CalculateSlotPositions(
            _currentFormation,
            _currentAnchorMode,
            anchorPosition,
            anchorForward,
            memberPositions);

        _squadBlackboard.Set(BBDataSig.FormationSlotPositions, slotPositions);
    }

    /// <summary>
    /// Reassigns formation slots for all members using the current strategy.
    /// </summary>
    private void ReassignSlots()
    {
        if (_currentFormation == null || _memberNodes.Count == 0)
        {
            return;
        }

        // Get current member positions
        var memberPositions = _memberNodes
            .Select(n => IsInstanceValid(n) ? n.GlobalPosition : Vector3.Zero)
            .ToList();

        // Calculate slot positions using formation controller
        // Use zero anchor for relative assignment - actual positions come from UpdateFormationPositions
        var slotPositions = FormationController.CalculateSlotPositions(
            _currentFormation,
            FormationAnchorMode.Leader,
            Vector3.Zero,
            Vector3.Forward);

        // Assign members to slots
        var assignments = _slotStrategy.AssignSlots(memberPositions, slotPositions, _leaderMemberIndex);

        // Update each member's blackboard with their slot assignment
        foreach (var assignment in assignments)
        {
            int memberIndex = assignment.Key;
            int slotIndex = assignment.Value;

            if (memberIndex >= 0 && memberIndex < _memberBlackboards.Count)
            {
                _memberBlackboards[memberIndex].Set(BBDataSig.FormationSlotIndex, slotIndex);
            }
        }
    }

    #endregion

    #region Squad State

    /// <summary>
    /// Periodically update the squad's shared state. Call from a Timer.
    /// </summary>
    public void UpdateSquadBlackboard()
    {
        if (_squadBlackboard == null || _memberBlackboards.Count == 0)
        {
            return;
        }

        float averageHealth = CalculateAverageHealth();
        _squadBlackboard.Set(BBDataSig.SquadAverageHealth, averageHealth);

        // Set squad behavior tag based on state
        _squadBlackboard.Set(BBDataSig.HasSquadTag, true);

        if (averageHealth < _panicHealthThreshold)
        {
            _squadBlackboard.Set(BBDataSig.ActiveSquadTag, _highThreatTag);
        }
        else
        {
            _squadBlackboard.Set(BBDataSig.ActiveSquadTag, _attackTag);
        }
    }

    private float CalculateAverageHealth()
    {
        if (_memberBlackboards.Count == 0)
        {
            return 0.5f;
        }

        float totalHealth = 0f;
        int validCount = 0;

        foreach (var bb in _memberBlackboards)
        {
            if (bb.TryGet<Core.Health.IHealth>(BBDataSig.HealthComponent, out var health) && health != null)
            {
                totalHealth += health.CurrentHealth / health.MaxHealth;
                validCount++;
            }
        }

        return validCount > 0 ? totalHealth / validCount : 0.5f;
    }

    #endregion
}
