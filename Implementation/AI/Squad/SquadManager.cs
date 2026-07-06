// --- SquadManager.cs ---
namespace Jmodot.Implementation.AI.Squad;

using System.Collections.Generic;
using System.Linq;
using Godot;
using BB;
using Core.AI.BB;
using Core.AI.Squad;
using Shared;

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

    private BlackboardGraph? _squadGraph;
    private List<IBlackboardGraph> _memberGraphs = new();
    private List<Node3D> _memberNodes = new();

    /// <summary>
    /// Read-only view of current squad members.
    /// </summary>
    public IReadOnlyList<Node3D> Members => _memberNodes;

    // Formation state
    private FormationDefinition? _currentFormation;
    private FormationAnchorMode _currentAnchorMode;
    private ISlotAssignmentStrategy _slotStrategy = new NearestSlotStrategy();
    private int _leaderMemberIndex = -1;

    public override void _Ready()
    {
        // Try to find graph child - may be pre-added or added later
        if (this.TryGetFirstChildOfType<BlackboardGraph>(out var graph))
        {
            _squadGraph = graph;
        }

        // Initialize with default formation if set
        if (_defaultFormation != null)
        {
            SetFormation(_defaultFormation, _anchorMode);
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetSquadGraph(BlackboardGraph graph) => _squadGraph = graph;
#endif
    #endregion

    #region Member Management

    /// <summary>
    /// Adds a member to the squad and assigns them a formation slot.
    /// </summary>
    /// <param name="memberNode">The Node3D representing the squad member.</param>
    /// <param name="isLeader">If true, this member becomes the formation leader (slot 0).</param>
    public void AddMember(Node3D memberNode, bool isLeader = false)
    {
        var graph = memberNode.GetGraph();
        if (graph == null)
        {
            JmoLogger.Warning(this, $"Cannot add member {memberNode.Name}: no BlackboardGraph found");
            return;
        }

        AddMemberInternal(memberNode, graph, isLeader);
    }

    /// <summary>
    /// Adds a member with an explicitly provided graph.
    /// Useful for testing or when graph is not a direct child.
    /// </summary>
    public void AddMember(Node3D memberNode, IBlackboardGraph memberGraph, bool isLeader = false)
    {
        AddMemberInternal(memberNode, memberGraph, isLeader);
    }

    private void AddMemberInternal(Node3D memberNode, IBlackboardGraph graph, bool isLeader)
    {
        _memberNodes.Add(memberNode);
        _memberGraphs.Add(graph);
        if (_squadGraph != null)
        {
            graph.AttachParent(_squadGraph);
        }

        if (isLeader)
        {
            _leaderMemberIndex = _memberNodes.Count - 1;
            _squadGraph?.Local.Set(BBDataSig.FormationLeader, memberNode);
        }

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

        var graph = _memberGraphs[index];
        graph.DetachParent();
        graph.Local.Set(BBDataSig.FormationSlotIndex, -1);

        _memberNodes.RemoveAt(index);
        _memberGraphs.RemoveAt(index);

        if (_leaderMemberIndex == index)
        {
            _leaderMemberIndex = -1;
            _squadGraph?.Local.Set<Node3D?>(BBDataSig.FormationLeader, null);
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

        _squadGraph?.Local.Set(BBDataSig.FormationActive, true);

        // Reassign slots for all existing members
        ReassignSlots();
    }

    /// <summary>
    /// Clears the current formation.
    /// </summary>
    public void ClearFormation()
    {
        _currentFormation = null;
        _squadGraph?.Local.Set(BBDataSig.FormationActive, false);
        _squadGraph?.Local.Set<Dictionary<int, Vector3>?>(BBDataSig.FormationSlotPositions, null);

        foreach (var g in _memberGraphs)
        {
            g.Local.Set(BBDataSig.FormationSlotIndex, -1);
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
        if (_currentFormation == null || _squadGraph == null)
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

        _squadGraph.Local.Set(BBDataSig.FormationSlotPositions, slotPositions);
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

            if (memberIndex >= 0 && memberIndex < _memberGraphs.Count)
            {
                _memberGraphs[memberIndex].Local.Set(BBDataSig.FormationSlotIndex, slotIndex);
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
        if (_squadGraph == null || _memberGraphs.Count == 0)
        {
            return;
        }

        float averageHealth = CalculateAverageHealth();
        _squadGraph.Local.Set(BBDataSig.SquadAverageHealth, averageHealth);
        _squadGraph.Local.Set(BBDataSig.HasSquadTag, true);

        if (averageHealth < _panicHealthThreshold)
        {
            _squadGraph.Local.Set(BBDataSig.ActiveSquadTag, _highThreatTag);
        }
        else
        {
            _squadGraph.Local.Set(BBDataSig.ActiveSquadTag, _attackTag);
        }
    }

    private float CalculateAverageHealth()
    {
        if (_memberGraphs.Count == 0)
        {
            return 0.5f;
        }

        float totalHealth = 0f;
        int validCount = 0;

        // Local-only intent: iterate members and ask each for their HealthComponent.
        // Squad does NOT walk down into members' BBs here — it walks the member list directly.
        foreach (var g in _memberGraphs)
        {
            if (g.Local.TryGet<Core.Health.IHealth>(BBDataSig.HealthComponent, out var health) && health != null)
            {
                totalHealth += health.CurrentHealth / health.MaxHealth;
                validCount++;
            }
        }

        return validCount > 0 ? totalHealth / validCount : 0.5f;
    }

    #endregion
}
