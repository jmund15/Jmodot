namespace Jmodot.Core.AI.BB;

/// <summary>
/// Scope-aware subscription mode for <see cref="IBlackboardGraphReadOnly.Subscribe"/>.
/// Selects which graph nodes a callback fires for, relative to the subscription site.
/// </summary>
/// <remarks>
/// Design reference: arch-bb-hierarchy §5 (Scope-aware subscriptions).
/// </remarks>
public enum ScopeWatchMode
{
    /// <summary>Fire only when the local leaf blackboard's key is set. Forwarded to <see cref="IBlackboardReadOnly"/>'s leaf subscription.</summary>
    LocalOnly,

    /// <summary>Fire when the local leaf sets the key, OR when any ancestor graph node sets the same key on its leaf.</summary>
    LocalOrAncestor,

    /// <summary>Fire when the local leaf sets the key, OR when any descendant graph node (current or attached later) sets the same key on its leaf.</summary>
    LocalOrDescendant,
}
