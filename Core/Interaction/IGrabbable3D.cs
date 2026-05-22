namespace Jmodot.Core.Interaction;

using System;
using Godot;
using Jmodot.Core.Shared;

/// <summary>
/// An object that can be grabbed via a three-step reserve→confirm→cancel handshake and
/// then held by an <see cref="IHolder3D"/>. The handshake is keyed on the holder interface
/// (not a concrete grabber) so any holder implementation can drive it.
/// </summary>
public interface IGrabbable3D : IInteractable3D, IGodotNodeInterface
{
    Node3D PhysicalBody { get; }
    bool IsGrabbable { get; }
    HoldAction PreferredHoldAction { get; }

    void DisableGrabbing();
    void EnableGrabbing();

    /// <summary>Step 1: a holder reserves this object if it is available.</summary>
    bool RequestGrab(IHolder3D holder);

    /// <summary>Step 2a: the reserving holder finalizes the grab.</summary>
    void ConfirmGrab(IHolder3D holder);

    /// <summary>Step 2b: the reserving holder releases the reservation (interrupted grab).</summary>
    void CancelGrab(IHolder3D holder);

    /// <summary>Hand control to another system without a physics reset.</summary>
    void RelinquishControl();

    event Action<Node3D> OnGrabbed;
}
