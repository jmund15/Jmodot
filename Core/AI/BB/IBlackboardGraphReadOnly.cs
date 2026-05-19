namespace Jmodot.Core.AI.BB;

using System;
using System.Collections.Generic;

/// <summary>
/// Read-only view of a blackboard graph node — local KV + topology + ancestor/descendant traversal,
/// but no writes anywhere reachable. <see cref="Local"/> returns <see cref="IBlackboardReadOnly"/>:
/// holding this reference (e.g. via <see cref="GetAncestor"/>) means cross-scope writes are
/// structurally impossible — there is no <c>Set</c> method to call.
/// </summary>
public interface IBlackboardGraphReadOnly
{
    IBlackboardReadOnly Local { get; }
    IBlackboardGraphReadOnly? Parent { get; }
    StringName? ScopeTag { get; }

    /// <summary>Walks the parent chain returning the first ancestor whose <see cref="ScopeTag"/> matches.</summary>
    IBlackboardGraphReadOnly? GetAncestor(StringName scopeTag);

    /// <summary>Yields self, then parent, then grandparent, ..., up to the root.</summary>
    IEnumerable<IBlackboardGraphReadOnly> AncestorChain();

    /// <summary>Direct children attached via <see cref="IBlackboardGraph.AttachParent"/>.</summary>
    IEnumerable<IBlackboardGraphReadOnly> Children { get; }

    /// <summary>Tries local first, then recursively up the parent chain.</summary>
    bool TryGetUp<T>(StringName key, out T? value);

    /// <summary>Folds across the ancestor chain (self → root), reading <paramref name="key"/> per scope.</summary>
    TAcc AggregateUp<TAcc>(StringName key, TAcc seed, Func<TAcc, object, TAcc> fold);

    /// <summary>Folds across self + all descendants (DFS), reading <paramref name="key"/> per scope.</summary>
    TAcc AggregateDown<TAcc>(StringName key, TAcc seed, Func<TAcc, object, TAcc> fold);

    /// <summary>
    /// Subscribe to set-events on <paramref name="key"/> across the scope-graph topology selected by <paramref name="mode"/>.
    /// See <see cref="ScopeWatchMode"/> for the LocalOnly / LocalOrAncestor / LocalOrDescendant semantics.
    /// Callback exception isolation matches <see cref="IBlackboardReadOnly"/>'s leaf subscription contract: one
    /// throwing subscriber must not break dispatch to other subscribers (design §12).
    /// </summary>
    void Subscribe(StringName key, Action<object> callback, ScopeWatchMode mode);

    /// <summary>Unsubscribe a callback previously registered with the matching <paramref name="key"/> and <paramref name="mode"/>.</summary>
    void Unsubscribe(StringName key, Action<object> callback, ScopeWatchMode mode);
}
