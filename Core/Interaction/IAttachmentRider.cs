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

    /// <summary>
    /// The pose used when <see cref="AttachPoses"/> is null, or null for a rider with no pose art at
    /// all. It sits BESIDE the set rather than inside it because a set-owned fallback is unreachable in
    /// the case it serves: a host books a pose from the set on every successful reserve, so the fallback
    /// is only ever wanted when there is no set to own it.
    /// </summary>
    /// <remarks>
    /// On the interface, not just the component, because the HOST reads it — a rider riding on default
    /// pose art must seat at that pose's offset like any other pose rider, and the host cannot honour a
    /// fallback it cannot see. Resolving it for clips alone would render the art off its authored spot.
    /// </remarks>
    AttachPose? DefaultPose { get; }

    /// <summary>The host whose anchor this rider is reserving or riding, or null when it holds neither.</summary>
    IAttachmentHost? Host { get; }

    /// <summary>True only while riding — that is, between a host's confirm and any detach. False for the whole approach.</summary>
    bool IsAttached { get; }

    /// <summary>
    /// Seconds since this rider was last shed, or <see cref="float.PositiveInfinity"/> if it never has
    /// been. Only a shed sets it; a deliberate detach does not, and re-attaching does not clear it.
    /// </summary>
    /// <remarks>
    /// Exposed as elapsed time rather than as a one-shot flag so the reader owns the window it cares
    /// about. A shed is the entity being thrown, which is a different fact from the size of the impulse
    /// that threw it — anything routing off "was thrown" must be able to ask directly, or it ends up
    /// inferring the event from a force threshold and silently stops firing when that force is tuned
    /// below the line.
    /// </remarks>
    float SecondsSinceShed { get; }

    /// <summary>
    /// True while a shed still bars this rider from claiming a host again. Only a shed arms it; a
    /// deliberate detach does not.
    /// </summary>
    /// <remarks>
    /// On the interface because the rider's OWN behaviour must be able to read it, not just the attach
    /// funnel that enforces it. A rider whose attach attempts are being silently refused has to route
    /// itself somewhere for the duration, and an AI that cannot see the refusal reason can only stand
    /// there re-attempting — the cooldown reads as a hang rather than as a recoil.
    /// </remarks>
    bool IsReattachOnCooldown { get; }

    /// <summary>
    /// The host booked <paramref name="localAnchor"/> (host-local, planar) for this rider. It may now
    /// fly there, but it is NOT attached yet — nothing that keys off riding may flip here.
    /// </summary>
    void OnReserved(IAttachmentHost host, Vector3 localAnchor);

    /// <summary>The rider arrived and the host confirmed it: the attachment is now real.</summary>
    void OnAttached(IAttachmentHost host, Vector3 localAnchor);

    /// <summary>
    /// Grip was exhausted: release positional authority FIRST, then convert the fling base into a
    /// launch impulse along <paramref name="direction"/>, attributed to
    /// <paramref name="attributedSource"/>. Ordering is load-bearing — an impulse applied while
    /// movement is still suspended is discarded.
    /// </summary>
    /// <param name="spentForce">How much of the blow's force this rider's grip absorbed.</param>
    /// <param name="attackKnockbackForce">The attack's own knockback force, when the attacker provides
    /// one — the fling scales from this. Zero falls back to <paramref name="spentForce"/>.</param>
    /// <param name="attributedSource">Who gets credit for the fling: the shed's instigator when one was named, else the host.</param>
    void OnShed(Vector3 direction, float spentForce, float attackKnockbackForce, Node? attributedSource);

    /// <summary>The attachment ended for a reason other than a shed; release every claim and resume normal AI.</summary>
    void OnDetached(DetachCause cause);

    /// <summary>
    /// Route a shed's damage payload through this rider's OWN hurtbox, so armour, reaction
    /// resolvers, payload interceptors and i-frames all run. The rider performs the call rather
    /// than the host reaching across into its components: only the rider resolved its own
    /// hurtbox from its own blackboard, and only it can tell an absent hurtbox from a rejected hit.
    /// </summary>
    /// <param name="impactDirection">The direction the blow travelled, as the host resolved it for this
    /// rider's fling. Carried into the hit so feedback keyed off the impact — fragment spray above all —
    /// is aimed by the blow rather than inferred from two bodies that overlap at damage time.</param>
    /// <returns>True when the hurtbox processed the hit; false when it rejected it or none exists.</returns>
    bool TryApplyShedDamage(IAttackPayload payload, Vector3? impactDirection = null);
}
