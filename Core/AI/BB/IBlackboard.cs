namespace Jmodot.Core.AI.BB;

/// <summary>
/// Local key-value store with no topology. For hierarchical scoping (parent/ancestor walk,
/// aggregation across scopes), use <see cref="IBlackboardGraph"/> — this interface is intentionally
/// flat. Read-only operations live on the <see cref="IBlackboardReadOnly"/> base.
/// </summary>
public interface IBlackboard : IBlackboardReadOnly
{
    /// <summary>
    /// Sets a value on this blackboard. Notifies local subscribers of <paramref name="key"/>.
    /// </summary>
    /// <remarks>
    /// <b>Null-storage asymmetry:</b> calling <c>Set&lt;T&gt;(key, null)</c> for a reference T
    /// persists as <c>Variant.Nil</c> in the underlying store. A subsequent
    /// <see cref="IBlackboardReadOnly.TryGet{T}"/> returns <c>false</c> (not "true with null value"),
    /// because the Nil variant fails the type cast for reference T. If a caller needs to
    /// distinguish "key absent" from "key present, value null", track the absence separately
    /// (e.g. via <see cref="IBlackboardReadOnly.ContainsLocal"/>) — do not rely on
    /// <c>TryGet</c> alone.
    /// <para>
    /// <b>Subscriber notification on null-write is intentional:</b> a null write still fires the
    /// key's subscribers (and the wildcard graph-dispatch hook) with a <c>null</c> payload. This
    /// is the canonical "value cleared" signal — consumers may act on it (e.g. resetting a latched
    /// flag, fanning a clear down the blackboard graph). It deliberately does NOT mirror the
    /// <c>TryGet</c>=false read semantics above: the producer-notify and reader-query sides are
    /// asymmetric by design.
    /// </para>
    /// </remarks>
    void Set<T>(StringName key, T value);
}
