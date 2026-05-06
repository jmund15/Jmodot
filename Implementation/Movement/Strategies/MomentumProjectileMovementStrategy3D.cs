namespace Jmodot.Implementation.Movement.Strategies;

using Core.Combat.EffectDefinitions;
using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
///     Projectile movement strategy that accelerates horizontally toward
///     <c>desiredDirection * _maxSpeed</c>, preserving the Y component of
///     <c>currentVelocity</c> so external gravity (added downstream by
///     <c>MovementProcessor3D.ApplyExternalForces</c>) is not erased.
///
///     <para>
///     Behaves like <see cref="LinearMovementStrategy3D"/> for impulses: a
///     perpendicular impulse deflects the projectile, and the strategy gradually
///     pulls horizontal velocity back toward the launch heading at
///     <c>_acceleration</c> units per second. Higher <c>_acceleration</c> = faster
///     snap-back (heavy projectiles); lower = longer carry (light projectiles).
///     </para>
///
///     <para>
///     <b>Contract:</b> <c>desiredDirection</c> is a normalized unit vector
///     (family convention shared with <see cref="LinearMovementStrategy3D"/>).
///     This DIFFERS from <see cref="ProjectileStrategy"/>, which expects
///     <c>desiredDirection</c> to arrive pre-scaled as a velocity.
///     </para>
///
///     <para>
///     <b>Use this when</b> a projectile must respect external impulses (Wind Blast,
///     knockback, force zones). For zero-momentum instant-snap velocity, use
///     <see cref="ProjectileStrategy"/> instead.
///     </para>
/// </summary>
[GlobalClass, Tool]
public partial class MomentumProjectileMovementStrategy3D : BaseMovementStrategy3D
{
    [ExportGroup("Value Definitions")]
    [Export, RequiredExport] private BaseFloatValueDefinition _maxSpeed = null!;
    [Export, RequiredExport] private BaseFloatValueDefinition _acceleration = null!;

    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection,
        Vector3 previousDirection, IStatProvider stats, float delta)
    {
        var maxSpeed = _maxSpeed.ResolveFloatValue(stats);
        var accel = _acceleration.ResolveFloatValue(stats);

        var horizontalCurrent = new Vector3(currentVelocity.X, 0f, currentVelocity.Z);
        var horizontalDir = new Vector3(desiredDirection.X, 0f, desiredDirection.Z);
        var horizontalTarget = horizontalDir * maxSpeed;

        var horizontalNew = horizontalCurrent.MoveToward(horizontalTarget, accel * delta);

        return new Vector3(horizontalNew.X, currentVelocity.Y, horizontalNew.Z);
    }
}
