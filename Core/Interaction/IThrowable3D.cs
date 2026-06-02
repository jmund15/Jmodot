namespace Jmodot.Core.Interaction;

using System;
using Godot;

/// <summary>
/// Capability for a held object that can be thrown with a launch velocity.
/// Split out of the former <c>IReleasable3D</c> so throw and drop compose independently.
/// </summary>
public interface IThrowable3D
{
    /// <summary>
    /// Release this held object as a throw, applying <see cref="ReleasePayload.LaunchVelocity"/>
    /// to its body (if launchable) and firing the release events. Replaces the former
    /// <c>Throw(Vector3)</c> arity so callers can carry classification + thrower stats.
    /// </summary>
    void Throw(ReleasePayload payload);

    /// <summary>
    /// Legacy throw notification carrying only the launch velocity. Preserved for existing
    /// subscribers; new listeners should prefer <see cref="OnThrownWithPayload"/>.
    /// </summary>
    event Action<Node3D, Vector3> OnThrown;

    /// <summary>
    /// Typed throw notification carrying the full <see cref="ReleasePayload"/> (kind, launch
    /// velocity, thrower stats). Fired alongside <see cref="OnThrown"/> on every throw.
    /// </summary>
    event Action<Node3D, ReleasePayload> OnThrownWithPayload;
}
