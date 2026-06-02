namespace Jmodot.Implementation.Interaction;

using Godot;
using Jmodot.Core.Interaction;
using Jmodot.Core.Movement;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
/// Release verb that throws the held item with a launch velocity derived from the holder's
/// aim direction scaled by <see cref="ThrowForce"/>. Requires the held grabbable to be
/// <see cref="IThrowable3D"/>.
/// </summary>
[GlobalClass]
public partial class ThrowReleaseVerb : ReleaseVerb
{
    /// <summary>Launch speed applied along the holder's aim direction (units/sec), not a physics force.</summary>
    [Export(PropertyHint.Range, "0.1,100,0.1")] public float ThrowForce { get; private set; } = 10f;

    // Flyability == launchability: only a body that can receive a launch impulse is throwable.
    // A StaticBody3D (or any body not implementing ILaunchable3D) gates out.
    public override bool CanRelease(IGrabbable3D grabbable)
        => grabbable is IThrowable3D && grabbable.PhysicalBody is ILaunchable3D;

    public override void Release(IHolder3D holder, IGrabbable3D grabbable)
    {
        // A non-positive launch speed yields a zero/backward throw — a designer misconfiguration,
        // not a runtime condition. Fail fast rather than silently launching wrong.
        if (ThrowForce <= 0f)
        {
            throw new ResourceConfigurationException(
                $"ThrowForce must be greater than 0 (was {ThrowForce}).", this);
        }

        // Throw BEFORE StopHolding: StopHolding reparents the body, whose _ExitTree unsubscribes
        // the OnThrown handlers. Stopping first would fire Throw into zero subscribers.
        if (grabbable is IThrowable3D throwable)
        {
            var payload = new ReleasePayload(
                ReleaseKind.Throw,
                holder.GetDirection().GetFlatVector3() * ThrowForce,
                holder.StatProvider);
            throwable.Throw(payload);
        }
        holder.StopHolding(grabbable.PhysicalBody);
    }

    #region Test Helpers
#if TOOLS
    internal void SetThrowForceForTest(float value) => ThrowForce = value;
#endif
    #endregion
}
