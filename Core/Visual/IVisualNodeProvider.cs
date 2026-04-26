namespace Jmodot.Core.Visual;

using System;
using System.Collections.Generic;

/// <summary>
/// Producer of <see cref="VisualNodeHandle"/>s. Replaces the legacy
/// <c>IVisualSpriteProvider</c> with a query-based, typed-handle API.
/// </summary>
/// <remarks>
/// Implementers MUST update internal state BEFORE firing change events — subscribers
/// calling <see cref="GetVisualNodes"/> from inside an event handler are guaranteed
/// to see consistent state.
/// </remarks>
public interface IVisualNodeProvider
{
    /// <summary>
    /// Returns the subset of this provider's handles matching the query, regardless of
    /// per-handle visibility.
    /// </summary>
    IReadOnlyList<VisualNodeHandle> GetVisualNodes(VisualQuery query);

    /// <summary>
    /// Returns the subset of this provider's handles matching the query AND currently
    /// visible. A handle is "visible" when its node is in the tree and any visibility
    /// coordinator has not hidden it.
    /// </summary>
    IReadOnlyList<VisualNodeHandle> GetVisibleNodes(VisualQuery query);

    /// <summary>
    /// Fired when a new handle becomes part of this provider's set.
    /// </summary>
    event Action<VisualNodeHandle> NodeAdded;

    /// <summary>
    /// Fired when a handle is removed from this provider's set. The handle's
    /// <c>Node</c> may already be queued for free.
    /// </summary>
    event Action<VisualNodeHandle> NodeRemoved;

    /// <summary>
    /// Fired when an existing handle's visibility changes.
    /// </summary>
    event Action<VisualNodeHandle> NodeVisibilityChanged;
}
