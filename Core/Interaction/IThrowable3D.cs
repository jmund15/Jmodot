namespace Jmodot.Core.Interaction;

using System;
using Godot;

/// <summary>
/// Capability for a held object that can be thrown with a launch velocity.
/// Split out of the former <c>IReleasable3D</c> so throw and drop compose independently.
/// </summary>
public interface IThrowable3D
{
    void Throw(Vector3 throwVelocity);
    event Action<Node3D, Vector3> OnThrown;
}
