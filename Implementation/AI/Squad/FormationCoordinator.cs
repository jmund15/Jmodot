namespace Jmodot.Implementation.AI.Squad;

using System.Collections.Generic;
using System.Linq;
using Godot;
using BB;
using Core.AI.BB;
using Core.AI.Squad;
using Core.Shared.Attributes;
using Shared;

/// <summary>
/// Formation half of the squad stack: reads <see cref="SquadRoster"/> membership and reacts to its
/// events, publishing formation slot data onto the squad and member blackboards. Holds no member
/// list of its own — all member/graph access goes through the roster (index-aligned lists), and the
/// squad graph is resolved lazily per use via <see cref="SquadRoster.SquadGraph"/> (never cached).
/// </summary>
[GlobalClass]
public partial class FormationCoordinator : Node
{
    [Export, RequiredExport] private SquadRoster _roster = null!;
    [Export] private FormationDefinition? _defaultFormation;
    [Export] private FormationAnchorMode _anchorMode = FormationAnchorMode.Leader;

    private FormationDefinition? _currentFormation;
    private FormationAnchorMode _currentAnchorMode;
    private ISlotAssignmentStrategy _slotStrategy = new NearestSlotStrategy();

    public override void _EnterTree()
    {
        if (_roster == null)
        {
            return; // _Ready's ValidateRequiredExports raises the friendly error
        }

        _roster.MemberAdded += OnMemberAdded;
        _roster.MemberRemoved += OnMemberRemoved;
        _roster.LeaderChanged += OnLeaderChanged;
    }

    public override void _ExitTree()
    {
        if (_roster == null)
        {
            return;
        }

        _roster.MemberAdded -= OnMemberAdded;
        _roster.MemberRemoved -= OnMemberRemoved;
        _roster.LeaderChanged -= OnLeaderChanged;
    }

    public override void _Ready()
    {
        this.ValidateRequiredExports();

        if (_defaultFormation != null)
        {
            SetFormation(_defaultFormation, _anchorMode);
        }
    }

    /// <summary>Sets the current formation and triggers slot reassignment.</summary>
    public void SetFormation(FormationDefinition formation, FormationAnchorMode anchorMode)
    {
        _currentFormation = formation;
        _currentAnchorMode = anchorMode;

        _roster.SquadGraph?.Local.Set(BBDataSig.FormationActive, true);

        ReassignSlots();
    }

    /// <summary>Clears the current formation and resets every member's slot index.</summary>
    public void ClearFormation()
    {
        _currentFormation = null;
        _roster.SquadGraph?.Local.Set(BBDataSig.FormationActive, false);
        _roster.SquadGraph?.Local.Set<Dictionary<int, Vector3>?>(BBDataSig.FormationSlotPositions, null);

        foreach (var g in _roster.MemberGraphs)
        {
            g.Local.Set(BBDataSig.FormationSlotIndex, -1);
        }
    }

    /// <summary>Updates formation slot world positions based on anchor and forward direction.</summary>
    public void UpdateFormationPositions(Vector3 anchorPosition, Vector3 anchorForward)
    {
        var squadGraph = _roster.SquadGraph;
        if (_currentFormation == null || squadGraph == null)
        {
            return;
        }

        List<Vector3>? memberPositions = null;
        if (_currentAnchorMode == FormationAnchorMode.Centroid)
        {
            memberPositions = _roster.Members
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

        squadGraph.Local.Set(BBDataSig.FormationSlotPositions, slotPositions);
    }

    /// <summary>Reassigns formation slots for all members using the current strategy.</summary>
    private void ReassignSlots()
    {
        if (_currentFormation == null || _roster.Members.Count == 0)
        {
            return;
        }

        var memberPositions = _roster.Members
            .Select(n => IsInstanceValid(n) ? n.GlobalPosition : Vector3.Zero)
            .ToList();

        // Zero anchor for relative assignment — world positions come from UpdateFormationPositions.
        var slotPositions = FormationController.CalculateSlotPositions(
            _currentFormation,
            FormationAnchorMode.Leader,
            Vector3.Zero,
            Vector3.Forward);

        int leaderIndex = ResolveLeaderIndex();
        var assignments = _slotStrategy.AssignSlots(memberPositions, slotPositions, leaderIndex);

        foreach (var assignment in assignments)
        {
            int memberIndex = assignment.Key;
            int slotIndex = assignment.Value;

            if (memberIndex >= 0 && memberIndex < _roster.MemberGraphs.Count)
            {
                _roster.MemberGraphs[memberIndex].Local.Set(BBDataSig.FormationSlotIndex, slotIndex);
            }
        }
    }

    private int ResolveLeaderIndex()
    {
        var leader = _roster.Leader;
        if (leader == null)
        {
            return -1;
        }

        for (int i = 0; i < _roster.Members.Count; i++)
        {
            if (ReferenceEquals(_roster.Members[i], leader))
            {
                return i;
            }
        }

        return -1;
    }

    private void OnMemberAdded(Node3D member) => ReassignSlots();

    private void OnMemberRemoved(Node3D member)
    {
        // The member graph is already detached from the squad graph; the local write is still valid.
        member.GetGraph()?.Local.Set(BBDataSig.FormationSlotIndex, -1);
        ReassignSlots();
    }

    private void OnLeaderChanged(Node3D? leader)
    {
        _roster.SquadGraph?.Local.Set<Node3D?>(BBDataSig.FormationLeader, leader);
        ReassignSlots();
    }
}
