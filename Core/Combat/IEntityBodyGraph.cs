namespace Jmodot.Core.Combat;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Exposes the other live nodes that constitute an entity for combat purposes.
/// </summary>
/// <remarks>
/// The enumeration is live, may be empty, may contain freed nodes, and has no ordering guarantee.
/// Callers must filter with <see cref="GodotObject.IsInstanceValid(GodotObject)"/> before using a node.
/// </remarks>
public interface IEntityBodyGraph
{
    /// <summary>Returns the entity's current body-graph nodes beyond its own root.</summary>
    IEnumerable<Node> BodyGraphNodes { get; }

    /// <summary>
    /// True when the members are this entity's own body, so a hit on one is a hit on the entity
    /// (segments). False when they are separate entities it carries (attached riders), which keep
    /// their hits to themselves.
    /// </summary>
    bool MemberHitsAreOwn { get; }

    /// <summary>
    /// Raised synchronously after membership changes; subscribers may enumerate the updated roster
    /// inside the callback.
    /// </summary>
    event Action BodyGraphChanged;
}
