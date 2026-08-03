namespace Jmodot.Core.Interaction;

using Godot;
using Jmodot.Implementation.Interaction.Attachment;

/// <summary>
/// A host's live bookkeeping for one attached rider. A value type carried by the host and consumed
/// by the pure shed resolver — <see cref="RemainingGrip"/> is a per-attachment budget that only ever
/// decreases, so force spent across separate hits accumulates against the same record.
/// </summary>
/// <param name="Rider">The attached rider this record accounts for.</param>
/// <param name="AttachSequence">Monotonic attach order on this host. Breaks ties in remaining grip so shed ordering stays deterministic.</param>
/// <param name="RemainingGrip">Force still required to shed this rider. Never regenerates.</param>
/// <param name="Footprint">How much of the host's capacity budget this rider occupies. Capacity accounting is by footprint sum, never rider count.</param>
/// <param name="LocalAnchor">Entity-local ride position on the host, measured from the host's visual-bounds centre. Planar — Z is always zero.</param>
/// <param name="Phase">Whether the rider has arrived yet. Records start <see cref="AttachmentPhase.Reserved"/> and the host advances them on confirm.</param>
/// <param name="Pose">The attach visual this rider holds on this host, or null for a rider with no pose art. The SINGLE home for the assignment: the rider derives its own pose by finding this record, so there is no second copy to forget clearing, and the pose frees with the record.</param>
public readonly record struct AttachmentRecord(
    IAttachmentRider Rider,
    int AttachSequence,
    float RemainingGrip,
    float Footprint,
    Vector3 LocalAnchor,
    AttachmentPhase Phase = AttachmentPhase.Reserved,
    AttachPose? Pose = null);
