namespace Jmodot.Implementation.Combat.Status;

using Core.Actors;
using Core.Combat;
using Core.Movement.Strategies;
using Godot;
using Implementation.AI.BB;
using Implementation.Movement.Strategies;
using Shared;

/// <summary>
/// Status runner that pushes a movement strategy override onto the target's
/// <see cref="IMovementProcessor3D"/> via <c>SetStrategyOverride</c> for the duration
/// of the status, clearing it on stop.
/// </summary>
/// <remarks>
/// <para>
/// The framework primitive for "movement-feel-only" statuses (slow, haste,
/// swim-through-mud) where the entity continues to act and decide normally but
/// moves differently. NOT for AI-suppressing statuses like freeze/stun/root —
/// those use <c>BehaviorAlterationProfile</c> via <c>BehaviorSuppressedState</c>,
/// which calls the same processor API. The processor's slot is single-writer-at-a-time;
/// concurrent writers trigger a <c>JmoLogger.Warning</c> on conflict.
/// </para>
/// <para>
/// <b>MUTUAL EXCLUSIVITY (designer responsibility):</b> a status using this runner
/// must NOT overlap with a status using <c>BehaviorSuppressedState</c> on the same
/// entity. Both call <c>SetStrategyOverride</c>; concurrent writers will trigger the
/// runtime conflict warning, and <c>ClearStrategyOverride</c> ordering determines final
/// state. Author profile/runner pairs such that "movement-feel-only" and "AI-suppressing"
/// statuses cannot both be active simultaneously (different elemental categories, mutex
/// stack policies, etc.).
/// </para>
/// <para>
/// Mirrors the override-slot pattern used by <see cref="Jmodot.Implementation.AI.HSM.BTState"/>
/// and PushinPotions' <c>ThrustAttackAction</c>: any entity that wires up a
/// <c>MovementProcessor3D</c> (Wizard, every NPC) will obey the override automatically,
/// no per-entity wiring required.
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

    private bool _pushed;

    public override void Start(ICombatant target, HitContext context)
    {
        if (MovementStrategyOverride != null)
        {
            if (_pushed)
            {
                JmoLogger.Warning(this, "Start called twice without Stop. Override slot already pushed; ignoring redundant Start.");
            }
            else if (target.Blackboard.TryGet<IMovementProcessor3D>(BBDataSig.MovementProcessor, out var processor) && processor != null)
            {
                processor.SetStrategyOverride((IMovementStrategy3D)MovementStrategyOverride);
                _pushed = true;
            }
            else
            {
                JmoLogger.Error(this, "MovementOverrideStatusRunner has MovementStrategyOverride but target.Blackboard.MovementProcessor is not registered.");
            }
        }

        base.Start(target, context);
    }

    public override void Stop(bool wasDispelled = false)
    {
        if (_pushed && Target != null)
        {
            if (Target.Blackboard.TryGet<IMovementProcessor3D>(BBDataSig.MovementProcessor, out var processor) && processor != null)
            {
                processor.ClearStrategyOverride();
            }
            _pushed = false;
        }

        base.Stop(wasDispelled);
    }
}
