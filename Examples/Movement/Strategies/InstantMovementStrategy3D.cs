namespace Jmodot.Examples.Movement.Strategies;

using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Movement.Strategies;

/// <summary>
/// Truly instant movement: the character moves at exactly desiredDirection * maxSpeed
/// every frame. No friction, no acceleration ramp-up, no momentum.
/// Stopping is instant. Direction changes are instant.
/// Turn rate is NOT handled here — attach a TurnRateProfile on BaseMovementStrategy3D instead.
/// </summary>
[GlobalClass, Tool]
public partial class InstantMovementStrategy3D : BaseMovementStrategy3D
{
    [Export, RequiredExport] private Attribute _maxSpeedAttr = null!;

    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection,
        Vector3 previousDirection, IStatProvider stats, float delta)
    {
        var maxSpeed = stats.GetStatValue<float>(_maxSpeedAttr);
        var target = desiredDirection * maxSpeed;
        return new Vector3(target.X, currentVelocity.Y, target.Z);
    }
}
