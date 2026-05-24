namespace Jmodot.Implementation.Interaction;

using Godot;
using Jmodot.Core.Interaction;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Shared;

/// <summary>
/// Release verb that throws the held item with a launch velocity derived from the holder's
/// aim direction scaled by <see cref="ThrowForce"/>. Requires the held grabbable to be
/// <see cref="IThrowable3D"/>.
/// </summary>
[GlobalClass]
public partial class ThrowReleaseVerb : ReleaseVerb
{
    /// <summary>Launch speed applied along the holder's aim direction (units/sec), not a physics force.</summary>
    [Export] public float ThrowForce { get; private set; } = 10f;

    public override bool CanRelease(IGrabbable3D grabbable) => grabbable is IThrowable3D;

    public override void Release(IHolder3D holder, IGrabbable3D grabbable)
    {
        // Throw BEFORE StopHolding: StopHolding reparents the body, whose _ExitTree unsubscribes
        // the OnThrown handlers. Stopping first would fire Throw into zero subscribers.
        if (grabbable is IThrowable3D throwable)
        {
            throwable.Throw(holder.GetDirection().GetFlatVector3() * ThrowForce);
        }
        holder.StopHolding(grabbable.PhysicalBody);
    }
}
