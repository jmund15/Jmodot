namespace Jmodot.Core.Interaction;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.Combat;
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
    /// Reserve capacity for <paramref name="rider"/> and place its anchor, leaving the record
    /// <see cref="AttachmentPhase.Reserved"/>. Fails without side effects when the rider's footprint
    /// exceeds remaining capacity or no free anchor could be placed.
    /// <para>
    /// Booking is deliberately separate from arriving: the rider flies to the anchor it was given,
    /// and until <see cref="ConfirmAttach"/> it is not riding — it grips nothing and sheds nothing.
    /// </para>
    /// </summary>
    bool TryReserve(IAttachmentRider rider, out AttachmentRecord record);

    /// <summary>
    /// The reserved rider arrived: advance its record to <see cref="AttachmentPhase.Riding"/> and
    /// tell it the attachment is real. No-op + Warning for a rider this host holds no record for.
    /// </summary>
    void ConfirmAttach(IAttachmentRider rider);

    /// <summary>
    /// Release <paramref name="rider"/>'s record and its footprint. No-op when this host holds none.
    /// The cancel path for BOTH phases — an abandoned reservation is handed back through here.
    /// </summary>
    void Detach(IAttachmentRider rider, DetachCause cause);

    /// <summary>
    /// Spend <paramref name="request"/>'s force against RIDING grip and apply the resulting plan:
    /// damage the scope's set through each rider's hurtbox, then shed and fling the exhausted.
    /// Reserved records are excluded entirely — a rider still in flight grips nothing to spend force
    /// against, so a shed neither costs force on it nor cancels its reservation.
    /// </summary>
    ShedPlan ApplyShed(ShedRequest request);

    /// <summary>
    /// World-space position of <paramref name="rider"/>'s anchor this frame, for the rider to write
    /// its own position from. False when the rider is not attached to this host.
    /// </summary>
    bool TryGetAnchorWorldPosition(IAttachmentRider rider, out Vector3 worldPosition);

    /// <summary>
    /// Route a rider's ride damage through this host's OWN hurtbox, so armour, reactions, interceptors
    /// and i-frames all run. The mirror of <see cref="IAttachmentRider.TryApplyShedDamage"/>: a hurtbox
    /// is resolved from its owning entity's blackboard, so each side applies inbound damage to itself
    /// rather than reaching across into the other's components.
    /// </summary>
    /// <returns>True when the hurtbox processed the hit; false when it rejected it or none exists.</returns>
    bool TryApplyRideDamage(IAttackPayload payload);

    /// <summary>Raised after a rider's record is released, whatever the cause.</summary>
    event Action<IAttachmentRider, DetachCause> RiderDetached;
}
