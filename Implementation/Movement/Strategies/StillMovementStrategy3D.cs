namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;

/// <summary>
/// Movement strategy that zeroes horizontal (XZ) drive while preserving the input
/// <c>currentVelocity.Y</c>. Ignores desired direction, previous direction, stats, and
/// delta — XZ is forced to zero unconditionally, Y is owned by external forces (gravity)
/// and impulses (knockback) and passed through untouched.
/// <para>
/// <b>Typical use:</b> wire as a transient override on the entity's
/// <see cref="Jmodot.Core.Actors.IMovementProcessor3D"/> via
/// <c>SetStrategyOverride</c> (e.g., from a BT leaf action's movement-override export
/// or a <c>ControlLossStateBase</c>-derived HSM state) during phases where the entity
/// must visibly stop driving horizontally — attack telegraphs, stunned states, dialogue
/// camera holds, channeled ability windups, control-loss windows (KnockedUp, Fall,
/// Getup). <c>ClearStrategyOverride</c> returns the processor to its <c>Default</c>.
/// </para>
/// <para>
/// <b>Interaction with impulses (v6.1):</b> XZ-component impulses are discarded by
/// design (the halt semantic). Y-component impulses (vertical knockback, gravity)
/// pass through — the impulse-modified Y from
/// <see cref="Jmodot.Core.Actors.IMovementProcessor3D.ApplyImpulse"/> reaches the
/// receiver intact. Pre-v6.1 this strategy returned <see cref="Vector3.Zero"/>
/// unconditionally, silently discarding upward knockback impulses applied in the
/// same frame and breaking the airborne-state pipeline that wires this strategy as
/// the control-loss override.
/// </para>
/// <para>
/// No stat bindings required — the strategy has no tunable numeric parameters. Subclass
/// if you need a "slow halt" (damped horizontal deceleration) variant.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class StillMovementStrategy3D : BaseMovementStrategy3D
{
    public override Vector3 CalculateVelocity(
        Vector3 currentVelocity,
        Vector3 desiredDirection,
        Vector3 previousDirection,
        IStatProvider stats,
        float delta)
    {
        // "Still" = zero XZ drive, preserve Y. Gravity + impulses own Y; this strategy
        // is a horizontal-plane concept. For grounded idle states, gravity correctly
        // settles Y to 0 via the physics pipeline. For airborne control-loss states,
        // the inherited Y reflects the impulse arc until landing.
        return new Vector3(0f, currentVelocity.Y, 0f);
    }
}
