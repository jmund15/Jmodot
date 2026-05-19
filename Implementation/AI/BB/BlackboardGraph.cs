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
    private bool _anyKeyHooked;
    private readonly Dictionary<StringName, GraphKeyState> _keyStates = new();

    /// <summary>
    /// Production-safe programmatic initialization seam. Sets <see cref="ScopeTag"/> + leaf
    /// <see cref="Blackboard"/> without requiring scene-tree wiring. Call BEFORE
    /// <see cref="AttachParent"/> and BEFORE adding the node to a scene tree. Idempotent on
    /// already-initialized graphs throws — double-init is a programmer error.
    /// </summary>
    public void Initialize(StringName scopeTag, Blackboard leaf)
    {
        if (_local != null!)
        {
            throw new InvalidOperationException("BlackboardGraph already initialized.");
        }
        _scopeTag = scopeTag;
        _local = leaf;
        HookLocalAnyKeyChanged();
    }

    public override void _Ready()
    {
        if (_local == null!)
        {
            var found = this.GetFirstChildOfType<Blackboard>();
            if (found == null)
            {
                throw new NodeConfigurationException("BlackboardGraph requires a child Blackboard node", this);
            }
            _local = found;
        }
        HookLocalAnyKeyChanged();
    }

    private void HookLocalAnyKeyChanged()
    {
        if (_anyKeyHooked || _local == null!)
        {
            return;
        }
        _local.AnyKeyChanged += OnLocalAnyKeyChanged;
        _anyKeyHooked = true;
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

        if (_anyKeyHooked && _local != null!)
        {
            _local.AnyKeyChanged -= OnLocalAnyKeyChanged;
            _anyKeyHooked = false;
        }

        // Godot frees children when the parent node is freed — no need to QueueFree _local explicitly.
        if (GodotObject.IsInstanceValid(this))
        {
            QueueFree();
        }
    }

    public override void _ExitTree() => DisposeSubgraph();

    public void Subscribe(StringName key, Action<object> callback, ScopeWatchMode mode)
    {
        if (!_keyStates.TryGetValue(key, out var state))
        {
            state = new GraphKeyState();
            _keyStates[key] = state;
        }
        GetListForMode(state, mode).Add(callback);
    }

    public void Unsubscribe(StringName key, Action<object> callback, ScopeWatchMode mode)
    {
        if (!_keyStates.TryGetValue(key, out var state))
        {
            return;
        }
        GetListForMode(state, mode).Remove(callback);
        if (state.IsEmpty)
        {
            _keyStates.Remove(key);
        }
    }

    private static List<Action<object>> GetListForMode(GraphKeyState state, ScopeWatchMode mode) => mode switch
    {
        ScopeWatchMode.LocalOnly => state.LocalOnly,
        ScopeWatchMode.LocalOrAncestor => state.LocalOrAncestor,
        ScopeWatchMode.LocalOrDescendant => state.LocalOrDescendant,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown ScopeWatchMode."),
    };

    private void OnLocalAnyKeyChanged(StringName key, object value)
    {
        // Self: fire all three mode lists (local-set is the trigger for every mode).
        if (_keyStates.TryGetValue(key, out var selfState))
        {
            FireSnapshot(selfState.LocalOnly, value, key, "LocalOnly");
            FireSnapshot(selfState.LocalOrAncestor, value, key, "LocalOrAncestor(self)");
            FireSnapshot(selfState.LocalOrDescendant, value, key, "LocalOrDescendant(self)");
        }

        // Ancestors with LocalOrDescendant subscriptions for `key` — they watch for any
        // descendant (current node included) writing this key on its local.
        var ancestor = _parent;
        while (ancestor != null)
        {
            if (ancestor._keyStates.TryGetValue(key, out var aState))
            {
                FireSnapshot(aState.LocalOrDescendant, value, key, "LocalOrDescendant(ancestor)");
            }
            ancestor = ancestor._parent;
        }

        // Descendants with LocalOrAncestor subscriptions for `key` — they watch for any
        // ancestor (current node included) writing this key on its local.
        FanDownLocalOrAncestor(this, key, value);
    }

    private void FanDownLocalOrAncestor(BlackboardGraph node, StringName key, object value)
    {
        foreach (var child in node._children)
        {
            if (child._keyStates.TryGetValue(key, out var cState))
            {
                FireSnapshot(cState.LocalOrAncestor, value, key, "LocalOrAncestor(descendant)");
            }
            FanDownLocalOrAncestor(child, key, value);
        }
    }

    private void FireSnapshot(List<Action<object>> callbacks, object value, StringName key, string label)
    {
        if (callbacks.Count == 0)
        {
            return;
        }
        // Snapshot for re-entrancy safety — a callback may sub/unsub mid-dispatch.
        var snapshot = callbacks.ToArray();
        foreach (var cb in snapshot)
        {
            try
            {
                cb(value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var subscriberType = cb.Target?.GetType().FullName ?? "<static>";
                JmoLogger.Warning(this, $"[BlackboardGraph] {label} subscriber {subscriberType} for key '{key}' threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private sealed class GraphKeyState
    {
        public readonly List<Action<object>> LocalOnly = new();
        public readonly List<Action<object>> LocalOrAncestor = new();
        public readonly List<Action<object>> LocalOrDescendant = new();
        public bool IsEmpty => LocalOnly.Count == 0 && LocalOrAncestor.Count == 0 && LocalOrDescendant.Count == 0;
    }

    #region Test Helpers
#if TOOLS
    /// <summary>Test seam: inject the local Blackboard without scene-tree wiring (mirrors BlackboardTest's no-tree pattern).</summary>
    internal void InitializeForTesting(Blackboard local)
    {
        _local = local;
        HookLocalAnyKeyChanged();
    }

    internal void SetScopeTagForTesting(StringName? tag) => _scopeTag = tag;
#endif
    #endregion
}
