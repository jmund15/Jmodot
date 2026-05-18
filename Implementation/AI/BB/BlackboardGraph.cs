namespace Jmodot.Implementation.AI.BB;

using System;
using System.Collections.Generic;
using Godot;
using Core.AI.BB;
using Shared;
using Shared.GodotExceptions;

/// <summary>
/// Concrete graph-node wrapping a local <see cref="Blackboard"/> with hierarchical topology + aggregation.
/// Owners hold this as <see cref="IBlackboardGraph"/> (writer); consumers reached via
/// <see cref="GetAncestor"/> see <see cref="IBlackboardGraphReadOnly"/> (cross-scope writes structurally denied).
/// </summary>
[GlobalClass]
[Tool]
public partial class BlackboardGraph : Node, IBlackboardGraph
{
    [Export] private StringName? _scopeTag;

    private Blackboard _local = null!;
    private BlackboardGraph? _parent;
    private readonly List<BlackboardGraph> _children = new();
    private bool _disposed;

    public override void _Ready()
    {
        if (_local != null!)
        {
            return;
        }

        var found = this.GetFirstChildOfType<Blackboard>();
        if (found == null)
        {
            throw new NodeConfigurationException("BlackboardGraph requires a child Blackboard node", this);
        }
        _local = found;
    }

    public IBlackboard Local => _local;
    IBlackboardReadOnly IBlackboardGraphReadOnly.Local => _local;

    public IBlackboardGraphReadOnly? Parent => _parent;
    public StringName? ScopeTag => _scopeTag;
    public IEnumerable<IBlackboardGraphReadOnly> Children
    {
        get
        {
            foreach (var c in _children) { yield return c; }
        }
    }

    public IBlackboardGraphReadOnly? GetAncestor(StringName scopeTag)
    {
        var cursor = _parent;
        while (cursor != null)
        {
            if (cursor._scopeTag != null && cursor._scopeTag == scopeTag)
            {
                return cursor;
            }
            cursor = cursor._parent;
        }
        return null;
    }

    public IEnumerable<IBlackboardGraphReadOnly> AncestorChain()
    {
        IBlackboardGraphReadOnly? cursor = this;
        while (cursor != null)
        {
            yield return cursor;
            cursor = cursor.Parent;
        }
    }

    public bool TryGetUp<T>(StringName key, out T? value)
    {
        if (_local.TryGet(key, out value))
        {
            return true;
        }
        return _parent != null && _parent.TryGetUp(key, out value);
    }

    public TAcc AggregateUp<TAcc>(StringName key, TAcc seed, Func<TAcc, object, TAcc> fold)
    {
        var acc = seed;
        var cursor = (BlackboardGraph?)this;
        while (cursor != null)
        {
            if (cursor._local.TryGet<object>(key, out var v) && v != null)
            {
                acc = fold(acc, v);
            }
            cursor = cursor._parent;
        }
        return acc;
    }

    public TAcc AggregateDown<TAcc>(StringName key, TAcc seed, Func<TAcc, object, TAcc> fold)
    {
        var acc = seed;
        if (_local.TryGet<object>(key, out var v) && v != null)
        {
            acc = fold(acc, v);
        }
        foreach (var child in _children)
        {
            acc = child.AggregateDown(key, acc, fold);
        }
        return acc;
    }

    // Single-impl constraint: parent must be a BlackboardGraph (concrete). The interface
    // method accepts IBlackboardGraph for forward-compatibility, but the children-list
    // bookkeeping needed for AggregateDown/Children traversal lives on the concrete type
    // and is not exposed on the interface (would widen public surface to allow arbitrary
    // child-registration). Add a new impl → expose RegisterChild/UnregisterChild on the
    // interface as a deliberate widening, then drop this constraint.
    public void AttachParent(IBlackboardGraph parent)
    {
        if (_parent != null)
        {
            throw new InvalidOperationException("BlackboardGraph already attached; DetachParent first.");
        }
        if (parent is not BlackboardGraph concrete)
        {
            throw new NotSupportedException(
                $"AttachParent requires a {nameof(BlackboardGraph)} instance; got {parent.GetType().FullName}. "
                + "See BlackboardGraph.AttachParent doc for the single-impl constraint.");
        }
        // Cycle guard: walk parent's ancestor chain; reject if `this` already appears.
        var cursor = concrete;
        while (cursor != null)
        {
            if (ReferenceEquals(cursor, this))
            {
                throw new InvalidOperationException(
                    "AttachParent would create a cycle (proposed parent is a descendant of this graph).");
            }
            cursor = cursor._parent;
        }
        _parent = concrete;
        concrete._children.Add(this);
    }

    public void DetachParent()
    {
        if (_parent == null)
        {
            return;
        }
        _parent._children.Remove(this);
        _parent = null;
    }

    public void DisposeSubgraph()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // Snapshot — children mutate _children via DetachParent during their own dispose.
        var snapshot = new List<BlackboardGraph>(_children);
        foreach (var child in snapshot)
        {
            child.DisposeSubgraph();
        }

        DetachParent();

        // Godot frees children when the parent node is freed — no need to QueueFree _local explicitly.
        if (GodotObject.IsInstanceValid(this))
        {
            QueueFree();
        }
    }

    public override void _ExitTree() => DisposeSubgraph();

    #region Test Helpers
#if TOOLS
    /// <summary>Test seam: inject the local Blackboard without scene-tree wiring (mirrors BlackboardTest's no-tree pattern).</summary>
    internal void InitializeForTesting(Blackboard local) => _local = local;
    internal void SetScopeTagForTesting(StringName? tag) => _scopeTag = tag;
#endif
    #endregion
}
