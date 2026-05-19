namespace Jmodot.Core.AI.BB;

using System;

/// <summary>
/// Owner-facing graph node — adds write access to <see cref="Local"/> and topology mutation.
/// The <c>new</c> shadow on <see cref="Local"/> widens the return type from
/// <see cref="IBlackboardReadOnly"/> (base) to <see cref="IBlackboard"/> (writer) only for callers
/// holding this exact reference. Consumers reached via <see cref="IBlackboardGraphReadOnly.GetAncestor"/>
/// see the base read-only typing — cross-scope writes are not expressible.
/// </summary>
public interface IBlackboardGraph : IBlackboardGraphReadOnly
{
    new IBlackboard Local { get; }

    /// <summary>Attach this graph as a child of <paramref name="parent"/>. Throws if already attached.</summary>
    void AttachParent(IBlackboardGraph parent);

    /// <summary>Detach from current parent. Idempotent.</summary>
    void DetachParent();

    /// <summary>Recursively dispose this subgraph: detaches children, detaches self, frees the local blackboard, frees this node. Idempotent.</summary>
    void DisposeSubgraph();

    /// <summary>
    /// Mirrors <see cref="IBlackboardGraphReadOnly.Subscribe"/> on the writable interface — re-declared so callers
    /// holding the writable reference see Subscribe alongside the rest of the owner-facing surface. Identical contract.
    /// </summary>
    new void Subscribe(StringName key, Action<object> callback, ScopeWatchMode mode);

    /// <summary>Mirrors <see cref="IBlackboardGraphReadOnly.Unsubscribe"/> on the writable interface. Identical contract.</summary>
    new void Unsubscribe(StringName key, Action<object> callback, ScopeWatchMode mode);
}
