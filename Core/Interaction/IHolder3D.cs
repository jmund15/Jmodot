namespace Jmodot.Core.Interaction;

using System;
using Godot;
using Jmodot.Core.Shared;
using Jmodot.Core.Stats;

/// <summary>
/// A component that can physically hold a <see cref="Node3D"/> and drive the
/// reserve→confirm grab handshake against an <see cref="IGrabbable3D"/>.
/// Unifies the hold surface (<see cref="StartHolding"/>/<see cref="StopHolding"/>/direction)
/// with the grab-initiation surface (<see cref="RequestHold"/> + grab events).
/// Hand-only holders that never initiate grabs satisfy the grab members minimally
/// (<see cref="RequestHold"/> returns false; the grab events stay unsubscribed).
/// </summary>
public interface IHolder3D : IGodotNodeInterface
{
    Node3D? HeldNode { get; }

    /// <summary>
    /// Stats of the entity driving this holder, forwarded into a <see cref="ReleasePayload.Thrower"/>
    /// on release so downstream effects can scale by the thrower. Null when the holder's entity has
    /// no stats — a valid configuration, not an error.
    /// </summary>
    IStatProvider? StatProvider { get; }

    /// <summary>Begin the reserve→confirm handshake against <paramref name="grabbable"/>.</summary>
    bool RequestHold(IGrabbable3D grabbable);

    bool StartHolding(Node3D toHold, HoldAction action = HoldAction.Nothing);
    bool StopHolding(Node3D? toStopHold);
    void SetDirection(Vector2 direction);
    Vector2 GetDirection();

    event Action<IGrabbable3D> AttemptingGrab;
    event Action<IGrabbable3D> CompletedGrab;
}
