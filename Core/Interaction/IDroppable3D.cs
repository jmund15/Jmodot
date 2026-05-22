namespace Jmodot.Core.Interaction;

using System;
using Godot;

/// <summary>
/// Capability for a held object that can be dropped (released to rest in place).
/// Split out of the former <c>IReleasable3D</c> so throw and drop compose independently.
/// </summary>
public interface IDroppable3D
{
    void Drop();
    event Action<Node3D> OnDropped;
}
