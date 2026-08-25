namespace Jmodot.Core.Combat;

/// <summary>
/// The knockback-delivery contract of a 2D actor. Dimension-parallel sibling of
/// <see cref="IKnockbackReceiver3D" />; it carries no <c>preserveVertical</c> because top-down 2D
/// has no up axis to preserve.
/// </summary>
public interface IKnockbackReceiver2D
{
    /// <param name="direction">Normalized direction of the knockback.</param>
    /// <param name="force">Impulse magnitude.</param>
    void ApplyKnockback(Vector2 direction, float force);
}
