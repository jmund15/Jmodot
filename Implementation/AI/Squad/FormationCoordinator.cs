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
    /// <summary>The roster whose membership drives this coordinator. Required — assign the sibling SquadRoster.</summary>
    [Export, RequiredExport] private SquadRoster _roster = null!;

    /// <summary>Formation applied automatically at _Ready. Leave null to stay inert until a caller invokes SetFormation.</summary>
    [Export] private FormationDefinition? _defaultFormation;

    /// <summary>How the formation is anchored (Leader/Centroid/Static). Applies to the default formation and to SetFormation calls that pass it through.</summary>
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

        // Coordinate-space fix: memberPositions above are world-space (GlobalPosition), but the
        // slot set below is computed at anchor Vector3.Zero — comparing the two directly makes
        // NearestSlotStrategy's distances meaningless once the squad is far from the world origin.
        // Relativize member positions against their own centroid (Vector3.Zero when none are
        // valid) so both sides of the comparison live in the same zero-anchored frame.
        Vector3 centroid = Vector3.Zero;
        int validCount = 0;
        foreach (var n in _roster.Members)
        {
            if (IsInstanceValid(n))
            {
                centroid += n.GlobalPosition;
                validCount++;
            }
        }

        if (validCount > 0)
        {
            centroid /= validCount;
        }

        var relativeMemberPositions = memberPositions.Select(p => p - centroid).ToList();

        // Relative frame, centroid-origin: FormationAnchorMode.Leader here selects the local slot
        // layout basis (zero anchor, Vector3.Forward) for the relative-assignment slot set — it is
        // NOT the live formation's anchor mode, and is anchor-agnostic by design.
        var slotPositions = FormationController.CalculateSlotPositions(
            _currentFormation,
            FormationAnchorMode.Leader,
            Vector3.Zero,
            Vector3.Forward);

        int leaderIndex = ResolveLeaderIndex();
        var assignments = _slotStrategy.AssignSlots(relativeMemberPositions, slotPositions, leaderIndex);

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
        // A freed-member removal (roster's deferred TreeExiting-check path) reaches here with an
        // already-disposed member — GetGraph() would throw ObjectDisposedException and abort
        // before ReassignSlots() runs. Guard so the removal still triggers reassignment.
        if (GodotObject.IsInstanceValid(member))
        {
            // The member graph is already detached from the squad graph; the local write is still valid.
            member.GetGraph()?.Local.Set(BBDataSig.FormationSlotIndex, -1);
        }

        ReassignSlots();
    }

    private void OnLeaderChanged(Node3D? leader)
    {
        _roster.SquadGraph?.Local.Set<Node3D?>(BBDataSig.FormationLeader, leader);
        ReassignSlots();
    }
}
