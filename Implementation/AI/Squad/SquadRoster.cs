namespace Jmodot.Implementation.AI.Squad;

using System;
using System.Collections.Generic;
using Godot;
using BB;
using Core.AI.BB;
using Core.Health;
using Shared;

/// <summary>
/// Owns squad membership, the squad-level <see cref="BlackboardGraph"/>, and the member
/// death / tree-exit lifecycle. The formation half lives in <see cref="FormationCoordinator"/>,
/// which reads roster state and reacts to the membership events raised here.
/// </summary>
[GlobalClass]
public partial class SquadRoster : Node
{
    [Export] private bool _removeMembersOnDeath = true;

    private readonly List<Node3D> _members = new();
    private readonly List<IBlackboardGraph> _memberGraphs = new();
    private readonly Dictionary<Node3D, IHealth> _memberHealth = new();
    private readonly Dictionary<Node3D, Action<HealthChangeEventArgs>> _diedHandlers = new();
    private readonly Dictionary<Node3D, Callable> _treeExitingHandlers = new();
    private Node3D? _leader;
    private int _peakMemberCount;
    private BlackboardGraph? _squadGraph;
    private bool _disbanded;

    /// <summary>Read-only view of current squad members.</summary>
    public IReadOnlyList<Node3D> Members => _members;

    /// <summary>Member blackboard graphs, index-aligned with <see cref="Members"/>. Stack-facing (the coordinator writes formation slots through it).</summary>
    public IReadOnlyList<IBlackboardGraph> MemberGraphs => _memberGraphs;

    public Node3D? Leader => _leader;

    /// <summary>Monotonic high-water mark of member count; never reset.</summary>
    public int PeakMemberCount => _peakMemberCount;

    /// <summary>Lazily created, owner-managed floating squad graph. Stack components only; members read via TryGetUp.</summary>
    public BlackboardGraph? SquadGraph => EnsureGraph();

    public event Action<Node3D> MemberAdded = delegate { };
    public event Action<Node3D> MemberRemoved = delegate { };
    public event Action<Node3D?> LeaderChanged = delegate { };

    /// <summary>Adds a member, resolving its <see cref="BlackboardGraph"/> via the child-discovery path.</summary>
    public void AddMember(Node3D member, bool isLeader = false)
    {
        var graph = member.GetGraph();
        if (graph == null)
        {
            JmoLogger.Warning(this, $"[Squad] Cannot add member '{member.Name}': no BlackboardGraph found.");
            return;
        }

        AddMemberInternal(member, graph, isLeader);
    }

    /// <summary>Adds a member with an explicitly provided graph (testing / non-child graphs).</summary>
    public void AddMember(Node3D member, IBlackboardGraph memberGraph, bool isLeader = false)
        => AddMemberInternal(member, memberGraph, isLeader);

    private void AddMemberInternal(Node3D member, IBlackboardGraph graph, bool isLeader)
    {
        if (_disbanded)
        {
            JmoLogger.Warning(this, $"[Squad] Cannot add member '{member.Name}': the squad has been disbanded (terminal).");
            return;
        }

        if (_members.Contains(member))
        {
            JmoLogger.Warning(this, $"[Squad] Member '{member.Name}' is already in the squad; ignoring double-add.");
            return;
        }

        if (!member.IsInsideTree())
        {
            JmoLogger.Warning(this, $"[Squad] Cannot add member '{member.Name}': member is not inside the scene tree.");
            return;
        }

        // AttachParent BEFORE any bookkeeping: an already-attached member graph throws, and a
        // mid-method throw must never leave a half-registered member.
        var squadGraph = EnsureGraph();
        if (squadGraph != null)
        {
            try
            {
                graph.AttachParent(squadGraph);
            }
            catch (InvalidOperationException)
            {
                JmoLogger.Warning(this, $"[Squad] Member '{member.Name}' graph is already attached to another parent; remove it from its old squad first. Ignoring add.");
                return;
            }
        }

        _members.Add(member);
        _memberGraphs.Add(graph);

        // Death integration: resolve IHealth via the member graph and subscribe OnDied.
        if (graph.Local.TryGet<IHealth>(BBDataSig.HealthComponent, out var health) && health != null)
        {
            Action<HealthChangeEventArgs> diedHandler = _ => OnMemberDied(member);
            health.OnDied += diedHandler;
            _memberHealth[member] = health;
            _diedHandlers[member] = diedHandler;
        }
        else
        {
            JmoLogger.Warning(this, $"[Squad] Member '{member.Name}': no IHealth on its graph; death auto-removal is disabled for this member.");
        }

        // Always subscribe TreeExiting as a reparent-filtered safety net (QueueFree bypasses domain events).
        var treeExitingHandler = Callable.From(() => OnMemberTreeExiting(member));
        member.Connect(Node.SignalName.TreeExiting, treeExitingHandler);
        _treeExitingHandlers[member] = treeExitingHandler;

        if (_members.Count > _peakMemberCount)
        {
            _peakMemberCount = _members.Count;
        }

        if (isLeader)
        {
            if (_leader != null)
            {
                JmoLogger.Warning(this, $"[Squad] A leader ('{_leader.Name}') already exists; overwriting with '{member.Name}'.");
            }

            _leader = member;
            LeaderChanged(member);
        }

        MemberAdded(member);
    }

    /// <summary>
    /// Removes <paramref name="member"/> from the squad: detaches its blackboard graph from the
    /// squad graph and unsubscribes exactly the health-death and tree-exiting handlers that
    /// AddMember registered for it. Silent no-op when the member is not currently in the roster, or
    /// when the roster has been disbanded (terminal) — never raises a duplicate
    /// <see cref="MemberRemoved"/>. Removing the current <see cref="Leader"/> clears it and raises
    /// <see cref="LeaderChanged"/> with <c>null</c>.
    /// </summary>
    public void RemoveMember(Node3D member)
    {
        if (_disbanded)
        {
            return;
        }

        int index = _members.IndexOf(member);
        if (index < 0)
        {
            return;
        }

        var graph = _memberGraphs[index];
        UnsubscribeMember(member);
        graph.DetachParent();

        _members.RemoveAt(index);
        _memberGraphs.RemoveAt(index);

        if (ReferenceEquals(_leader, member))
        {
            _leader = null;
            LeaderChanged(null);
        }

        MemberRemoved(member);
    }

    /// <summary>
    /// Terminal teardown: unsubscribe every member (snapshot-first), detach member graphs, clear
    /// public state SILENTLY (no per-member events), then dispose the floating squad graph. Idempotent.
    /// </summary>
    public void Disband()
    {
        if (_disbanded)
        {
            return;
        }

        _disbanded = true;

        // Unsubscribe-first pass over a snapshot, so a reentrant OnDied cannot modify the list mid-teardown.
        var snapshot = new List<Node3D>(_members);
        foreach (var member in snapshot)
        {
            UnsubscribeMember(member);
        }

        // Detach member graphs BEFORE disposing the squad graph (survivors' graphs must not be recursively disposed).
        foreach (var graph in _memberGraphs)
        {
            graph.DetachParent();
        }

        _members.Clear();
        _memberGraphs.Clear();
        _leader = null;

        // Dispose via the private backing field only — never the getter (which would mint a fresh graph).
        if (_squadGraph != null && GodotObject.IsInstanceValid(_squadGraph))
        {
            _squadGraph.DisposeSubgraph();
        }
    }

    public override void _ExitTree()
    {
        // Stack exit is always teardown. A non-sanctioned exit (reparent) is loud but still disbands.
        if (!_disbanded && !AncestorQueuedForDeletion())
        {
            JmoLogger.Error(this, "[Squad] squad stack reparent is unsupported — squad disbanding.");
        }

        Disband();
    }

    private void OnMemberDied(Node3D member)
    {
        if (_disbanded)
        {
            return;
        }

        if (_removeMembersOnDeath)
        {
            RemoveMember(member);
        }
    }

    private void OnMemberTreeExiting(Node3D member)
    {
        // TreeExiting fires on reparent too — defer the decision and only remove if the member is truly gone.
        Callable.From(() => DeferredTreeExitCheck(member)).CallDeferred();
    }

    private void DeferredTreeExitCheck(Node3D member)
    {
        if (!GodotObject.IsInstanceValid(this) || _disbanded)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(member) || !member.IsInsideTree())
        {
            RemoveMember(member); // genuinely gone
        }
        // else: still in tree → reparented → retains membership
    }

    private void UnsubscribeMember(Node3D member)
    {
        if (_diedHandlers.TryGetValue(member, out var diedHandler)
            && _memberHealth.TryGetValue(member, out var health) && health != null)
        {
            health.OnDied -= diedHandler;
        }

        _diedHandlers.Remove(member);
        _memberHealth.Remove(member);

        if (_treeExitingHandlers.TryGetValue(member, out var treeExitingHandler))
        {
            if (GodotObject.IsInstanceValid(member))
            {
                member.Disconnect(Node.SignalName.TreeExiting, treeExitingHandler);
            }

            _treeExitingHandlers.Remove(member);
        }
    }

    private bool AncestorQueuedForDeletion()
    {
        Node? node = this;
        while (node != null)
        {
            if (node.IsQueuedForDeletion())
            {
                return true;
            }

            node = node.GetParent();
        }

        return false;
    }

    private BlackboardGraph? EnsureGraph()
    {
        if (_disbanded)
        {
            JmoLogger.Warning(this, "[Squad] SquadGraph accessed after Disband; returning null (the squad graph is never re-created).");
            return null;
        }

        if (_squadGraph == null)
        {
            if (this.TryGetFirstChildOfType<BlackboardGraph>(out _))
            {
                JmoLogger.Warning(this, "[Squad] A scene-authored BlackboardGraph child under SquadRoster is unsupported; using an owner-managed floating squad graph instead.");
            }

            var g = new BlackboardGraph();
            g.Initialize(new StringName("Squad"), new Blackboard());
            _squadGraph = g;
        }

        return _squadGraph;
    }
}
