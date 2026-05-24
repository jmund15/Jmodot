namespace Jmodot.Implementation.Interaction;

using Godot;
using Jmodot.Core.Interaction;

/// <summary>
/// Release verb that drops the held item at rest in place. Requires the held grabbable to be
/// <see cref="IDroppable3D"/>.
/// </summary>
[GlobalClass]
public partial class DropReleaseVerb : ReleaseVerb
{
    public override bool CanRelease(IGrabbable3D grabbable) => grabbable is IDroppable3D;

    public override void Release(IHolder3D holder, IGrabbable3D grabbable)
    {
        if (grabbable is IDroppable3D droppable)
        {
            droppable.Drop();
        }
        holder.StopHolding(grabbable.PhysicalBody);
    }
}
