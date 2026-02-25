namespace Jmodot.Core.Visual.DestroyStrategies;

using System;
using Godot;

/// <summary>
/// Abstract base class for node destruction strategies.
/// Defines how a Node3D should be destroyed (instant, fade, shatter, explosion, etc.)
/// with a callback for completion notification.
/// Implementations MUST invoke onFinished exactly once when destruction is complete.
/// </summary>
[GlobalClass, Tool]
public abstract partial class DestroyStrategy : Resource
{
    /// <summary>
    /// When true, the node's velocity should be preserved through destruction
    /// (e.g., for a fading projectile that continues moving).
    /// </summary>
    [Export] public bool KeepVelocity { get; private set; } = false;

    /// <summary>
    /// Execute the destruction of the given node.
    /// </summary>
    public abstract void Destroy(Node3D node, Action onFinished);
}
