namespace Jmodot.Core.Interaction;

using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared;
using Jmodot.Implementation.Interaction.Attachment;

/// <summary>
/// An entity that latches onto an <see cref="IAttachmentHost"/>, rides it, and damages it over time.
/// Unlike <see cref="IGrabbable3D"/> — a passive object driven through a reserve→confirm handshake —
/// a rider is autonomous: it initiates its own attachment and keeps running its own AI while
/// attached. It is never reparented onto the host; it stays a world-space sibling positioned from
/// the host's live anchor.
/// </summary>
public interface IAttachmentRider : IGodotNodeInterface
{
    /// <summary>How much of a host's capacity budget this rider occupies while attached.</summary>
    float Footprint { get; }

    /// <summary>Force required to shed this rider from a fresh attachment. Refills fully on each new attachment; never regenerates mid-ride.</summary>
    float MaxGrip { get; }

    /// <summary>Damage per second dealt to the host while attached.</summary>
    float AttachDamagePerSecond { get; }

    /// <summary>
    /// The attach visuals this rider's art provides, or null for a rider with no pose art — the whole
    /// pose mechanism is opt-in and a null set means the legacy behaviour everywhere. The set is
    /// RIDER-owned because the art is the rider's, so it travels to any host with zero host authoring;
    /// a host tracks only which of its <see cref="AttachPose.Id"/>s its current riders hold.
    /// </summary>
    AttachPoseSet? AttachPoses { get; }

    /// <summary>The host whose anchor this rider is reserving or riding, or null when it holds neither.</summary>
    IAttachmentHost? Host { get; }

    /// <summary>True only while riding — that is, between a host's confirm and any detach. False for the whole approach.</summary>
    bool IsAttached { get; }

    /// <summary>
    /// The host booked <paramref name="localAnchor"/> (host-local, planar) for this rider. It may now
    /// fly there, but it is NOT attached yet — nothing that keys off riding may flip here.
    /// </summary>
    void OnReserved(IAttachmentHost host, Vector3 localAnchor);

    /// <summary>The rider arrived and the host confirmed it: the attachment is now real.</summary>
    void OnAttached(IAttachmentHost host, Vector3 localAnchor);

    /// <summary>
    /// Grip was exhausted: release positional authority FIRST, then convert
    /// <paramref name="spentForce"/> into a launch impulse along <paramref name="direction"/>,
    /// attributed to <paramref name="attributedSource"/>. Ordering is load-bearing — an impulse
    /// applied while movement is still suspended is discarded.
    /// </summary>
    /// <param name="attributedSource">Who gets credit for the fling: the shed's instigator when one was named, else the host.</param>
    void OnShed(Vector3 direction, float spentForce, Node? attributedSource);

    /// <summary>The attachment ended for a reason other than a shed; release every claim and resume normal AI.</summary>
    void OnDetached(DetachCause cause);

    /// <summary>
    /// Route a shed's damage payload through this rider's OWN hurtbox, so armour, reaction
    /// resolvers, payload interceptors and i-frames all run. The rider performs the call rather
    /// than the host reaching across into its components: only the rider resolved its own
    /// hurtbox from its own blackboard, and only it can tell an absent hurtbox from a rejected hit.
    /// </summary>
    /// <returns>True when the hurtbox processed the hit; false when it rejected it or none exists.</returns>
    bool TryApplyShedDamage(IAttackPayload payload);
}
