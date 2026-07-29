namespace Jmodot.Core.Interaction;

using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Shared;

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

    /// <summary>Multiplier converting force spent shedding this rider into its launch impulse.</summary>
    float FlingForceScale { get; }

    /// <summary>Damage per second dealt to the host while attached.</summary>
    float AttachDamagePerSecond { get; }

    /// <summary>The host being ridden, or null while unattached.</summary>
    IAttachmentHost? Host { get; }

    /// <summary>True between a successful attach and any detach.</summary>
    bool IsAttached { get; }

    /// <summary>The host confirmed the attachment at <paramref name="localAnchor"/> (host-local, planar).</summary>
    void OnAttached(IAttachmentHost host, Vector3 localAnchor);

    /// <summary>
    /// Grip was exhausted: release positional authority FIRST, then apply
    /// <paramref name="spentForce"/> × <see cref="FlingForceScale"/> along <paramref name="direction"/>.
    /// Ordering is load-bearing — an impulse applied while movement is still suspended is discarded.
    /// </summary>
    void OnShed(Vector3 direction, float spentForce);

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
