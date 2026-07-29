namespace Jmodot.Core.Interaction;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.Shared;

/// <summary>
/// An entity that can carry <see cref="IAttachmentRider"/>s and shake them off. Distinct from
/// <see cref="IHolder3D"/>, whose single <c>HeldNode</c> slot cannot express multi-occupancy against
/// a capacity budget: a host carries as many riders as their summed <see cref="IAttachmentRider.Footprint"/>
/// fits, never a rider count.
/// </summary>
public interface IAttachmentHost : IGodotNodeInterface
{
    /// <summary>Total footprint this host can carry at once.</summary>
    float Capacity { get; }

    /// <summary>Footprint currently consumed by attached riders.</summary>
    float UsedFootprint { get; }

    /// <summary>Live records for every attached rider, in attach order.</summary>
    IReadOnlyList<AttachmentRecord> Attachments { get; }

    /// <summary>
    /// Reserve capacity for <paramref name="rider"/> and place its anchor. Fails without side effects
    /// when the rider's footprint exceeds remaining capacity or no free anchor could be placed.
    /// </summary>
    bool TryAttach(IAttachmentRider rider, out AttachmentRecord record);

    /// <summary>Release <paramref name="rider"/>'s record and its footprint. No-op when it is not attached.</summary>
    void Detach(IAttachmentRider rider, DetachCause cause);

    /// <summary>
    /// Spend <paramref name="request"/>'s force against attached grip and apply the resulting plan:
    /// damage the scope's set through each rider's hurtbox, then shed and fling the exhausted.
    /// </summary>
    ShedPlan ApplyShed(ShedRequest request);

    /// <summary>
    /// World-space position of <paramref name="rider"/>'s anchor this frame, for the rider to write
    /// its own position from. False when the rider is not attached to this host.
    /// </summary>
    bool TryGetAnchorWorldPosition(IAttachmentRider rider, out Vector3 worldPosition);

    /// <summary>Raised after a rider's record is released, whatever the cause.</summary>
    event Action<IAttachmentRider, DetachCause> RiderDetached;
}
