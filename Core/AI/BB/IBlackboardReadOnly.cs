namespace Jmodot.Core.AI.BB;

using System;

/// <summary>
/// Read-only view of a blackboard's local scope. Holding this reference grants the ability to
/// read local keys and observe local changes, but NOT to write or to walk to ancestors.
/// Ancestor traversal lives on <see cref="IBlackboardGraphReadOnly"/>.
/// </summary>
/// <remarks>
/// <para>Subscription contract is <b>local-only</b>: a <c>Set</c> on this blackboard notifies
/// subscribers registered here. Writes on an ancestor blackboard do NOT notify subscribers
/// of descendant blackboards; cross-scope reactive observation is a deferred concern.</para>
/// </remarks>
public interface IBlackboardReadOnly
{
    T Get<T>(StringName key);
    bool TryGet<T>(StringName key, out T? value);
    bool ContainsLocal(StringName key);
    void Subscribe(StringName key, Action<object> callback);
    void Unsubscribe(StringName key, Action<object> callback);
}
