namespace Jmodot.Core.Visual;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.Visual.Animation.Sprite;

/// <summary>
/// Typed handle for a single visual node managed by an <see cref="IVisualNodeProvider"/>.
/// Carries slot/part/tag identity so consumers can target nodes via <see cref="VisualQuery"/>
/// rather than walking raw <c>Node</c> lists.
/// </summary>
public sealed record VisualNodeHandle(
    SlotKey SlotId,
    StringName? PartId,
    IReadOnlySet<StringName> Tags,
    Node Node,
    IVisualNodeProvider OwningProvider,
    bool IsVisible);
