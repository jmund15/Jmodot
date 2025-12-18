namespace Jmodot.Core.Visual.Effects;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Interface for components that provide access to dynamic visual nodes (sprites).
/// Implemented by VisualComposer, AnimationVisibilityCoordinator, etc.
/// </summary>
public interface IVisualSpriteProvider
{
    /// <summary>
    /// Returns all currently active/visible visual nodes (Sprite3D, Sprite2D, etc.)
    /// </summary>
    IReadOnlyList<Node> GetVisibleNodes();

    /// <summary>
    /// Get all visual nodes (Sprite3D, Sprite2D, etc.) managed by this provider, regardless of visibility.
    /// </summary>
    IReadOnlyList<Node> GetAllVisualNodes();

    /// <summary>
    /// Fired when the set of active visual nodes changes (visibility toggle, equip/unequip, etc.)
    /// </summary>
    event Action VisibleNodesChanged;

    /// <summary>
    /// Fired when the set of visual nodes changes (new node added/removed from tree)
    /// </summary>
    event Action VisualNodesChanged;
}
