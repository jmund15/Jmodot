namespace Jmodot.Implementation.Combat.Status;

using Core.Combat;
using Core.Movement.Strategies;
using Godot;
using Implementation.AI.BB;
using Implementation.Movement.Strategies;

/// <summary>
/// Status runner that pushes a movement strategy override to
/// <see cref="BBDataSig.ActiveMovementStrategy"/> for the duration of the status,
/// then restores whatever was there before on stop.
/// </summary>
/// <remarks>
/// <para>
/// The framework primitive for "movement-feel-only" statuses (slow, haste,
/// swim-through-mud) where the entity continues to act and decide normally but
/// moves differently. NOT for AI-suppressing statuses like freeze/stun/root —
/// those use <c>BehaviorAlterationProfile</c> via <c>BehaviorSuppressedState</c>,
/// which owns the BB slot for those cases (see plan §"Coordination note: who
/// owns ActiveMovementStrategy?").
/// </para>
/// <para>
/// <b>MUTUAL EXCLUSIVITY (designer responsibility):</b> a status using this runner
/// must NOT overlap with a status using <c>BehaviorSuppressedState</c> on the same
/// entity. Both write to <see cref="BBDataSig.ActiveMovementStrategy"/> and cache
/// the prior strategy on Start; if interleaved (slow active → freeze enters →
/// slow expires → freeze exits), the entity's BB ends pointing at a freed runner's
/// strategy. The framework does not enforce this — author profile/runner pairs
/// such that "movement-feel-only" and "AI-suppressing" statuses cannot both be
/// active simultaneously (different elemental categories, mutex stack policies,
/// etc.).
/// </para>
/// <para>
/// Mirrors the cache+push+restore pattern from
/// <see cref="Jmodot.Implementation.AI.HSM.BTState"/> and PushinPotions'
/// <c>ThrustAttackAction</c>: any entity whose movement pipeline already respects
/// <see cref="BBDataSig.ActiveMovementStrategy"/> (Wizard, every NPC) will obey
/// the override automatically, no per-entity wiring required.
/// </para>
/// </remarks>
[GlobalClass]
public partial class MovementOverrideStatusRunner : DurationStatusRunner
{
    /// <summary>
    /// Strategy pushed to the target's BB on Start. Null = no override (the runner
    /// behaves as a plain DurationStatusRunner with no movement effect).
    /// </summary>
    [Export] public BaseMovementStrategy3D? MovementStrategyOverride { get; set; }

    private IMovementStrategy3D? _priorStrategy;
    private bool _pushed;

    public override void Start(ICombatant target, HitContext context)
    {
        if (MovementStrategyOverride != null && !_pushed)
        {
            target.Blackboard.TryGet<IMovementStrategy3D>(BBDataSig.ActiveMovementStrategy, out _priorStrategy);
            target.Blackboard.Set(BBDataSig.ActiveMovementStrategy, (IMovementStrategy3D)MovementStrategyOverride);
            _pushed = true;
        }

        base.Start(target, context);
    }

    public override void Stop(bool wasDispelled = false)
    {
        if (_pushed && Target != null)
        {
            Target.Blackboard.Set<IMovementStrategy3D?>(BBDataSig.ActiveMovementStrategy, _priorStrategy);
            _pushed = false;
            _priorStrategy = null;
        }

        base.Stop(wasDispelled);
    }
}
