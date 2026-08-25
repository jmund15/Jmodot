namespace Jmodot.Core.Combat;

/// <summary>
/// The knockback-delivery contract of a 3D actor, independent of body regime. Producers of a
/// fling — <see cref="Jmodot.Implementation.Interaction.Attachment.AttachmentRiderComponent3D" />
/// above all — resolve this off <c>BBDataSig.KnockbackComponent</c> rather than a concrete
/// component, so a CharacterBody3D and a RigidBody3D actor are equally flingable.
/// Dimension-parallel sibling: <see cref="IKnockbackReceiver2D" />.
/// </summary>
public interface IKnockbackReceiver3D
{
    /// <param name="direction">Normalized direction of the knockback.</param>
    /// <param name="incomingForce">Impulse magnitude in N·s.</param>
    /// <param name="attributedSource">Originating cause, for HSM transition / VFX / audio chain attribution.</param>
    /// <param name="preserveVertical">
    /// When true the impulse's vertical component is intentional and the receiver must not zero it.
    /// Receivers OR this with their own <c>PreserveVertical</c> export — either side may assert the
    /// vertical, neither may veto it.
    /// </param>
    void ApplyKnockback(Vector3 direction, float incomingForce, Node? attributedSource = null, bool preserveVertical = false);
}
