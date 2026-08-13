namespace Jmodot.Core.Environment.FlowField;

/// <summary>
///     Which of the two force seams a flow field drives. Closed set — these are the only two
///     aggregates <see cref="Jmodot.Implementation.Actors.ExternalForceReceiver3D"/> consumes for movement.
/// </summary>
public enum FlowForceFlavor
{
    /// <summary><see cref="IForceProvider3D" />: the sampled velocity is added as an environmental force.</summary>
    Force,

    /// <summary><see cref="IVelocityOffsetProvider3D" />: the sampled velocity carries the body (wave-style).</summary>
    VelocityOffset,
}
