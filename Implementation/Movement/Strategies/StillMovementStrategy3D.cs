namespace Jmodot.Implementation.Movement.Strategies;

using Core.Stats;

/// <summary>
/// Movement strategy that outputs <see cref="Vector3.Zero"/> unconditionally.
/// Ignores desired direction, current velocity, stats, and delta — the entity halts
/// and stays halted while this strategy is active.
/// <para>
/// <b>Typical use:</b> wire as a transient override on <see cref="Jmodot.Implementation.AI.BB.BBDataSig.ActiveMovementStrategy"/>
/// (e.g., via a BT leaf action's movement-override export) during phases where the
/// entity must visibly stop — attack telegraphs, stunned states, dialogue camera holds,
/// channeled ability windups. Restoring the prior strategy returns normal motion.
/// </para>
/// <para>
/// <b>Interaction with impulses:</b> this strategy REPLACES the currentVelocity input
/// rather than preserving it. If an impulse was applied this frame via
/// <see cref="Jmodot.Core.Actors.IMovementProcessor3D.ApplyImpulse"/>, the impulse is
/// effectively discarded because <see cref="CalculateVelocity"/> returns zero regardless.
/// This is intentional for the halt semantic — callers that want "preserve current motion"
/// should use a different strategy or no override.
/// </para>
/// <para>
/// No stat bindings required — the strategy has no tunable numeric parameters. Subclass
/// if you need a "slow halt" (damped deceleration) or "gravity-preserving halt" variant.
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
        return Vector3.Zero;
    }
}
