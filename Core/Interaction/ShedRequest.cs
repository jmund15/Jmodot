namespace Jmodot.Core.Interaction;

using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Stats;

/// <summary>
/// One forceful action asking a host to shake riders off. A runtime value (plain record, not a
/// <c>[GlobalClass]</c> resource — mirrors <see cref="ReleasePayload"/>): the melee state or cast
/// pipeline constructs it, the host resolves it, and nothing downstream couples to the caller.
/// </summary>
/// <param name="Force">Force available to spend against rider grip. Spent weakest-remaining-grip first; whatever is left after the last shed is discarded, never carried.</param>
/// <param name="DamagePayload">Damage effects applied to every rider in the scope's damage set, through that rider's hurtbox. Carries damage only — the fling impulse is derived from force spent, not from authored knockback. Null for a shed that only pushes.</param>
/// <param name="Scope">Which riders the payload reaches. Never affects how force is spent.</param>
/// <param name="OriginPosition">World-space origin of the action, used to aim each shed rider's fling away from it.</param>
/// <param name="Instigator">Who performed the action, for attributing each fling. Null attributes the fling to the host itself — right for a host shaking itself off, wrong for a third party slapping riders loose.</param>
/// <param name="InstigatorStats">Stats of the instigating entity, for downstream scaling. Null when the instigator has no stats — a valid state, not an error.</param>
/// <param name="ImpactDirection">The direction the blow travelled, supplied by the attacker. Used to aim a rider whose anchor cannot imply a direction — a rider seated at the host's own origin sits ON the origin position, so nothing about its seat says where the blow came from. Null when the attacker has no direction to give.</param>
public record ShedRequest(
    float Force,
    IAttackPayload? DamagePayload,
    ShedDamageScope Scope,
    Vector3 OriginPosition,
    Node? Instigator = null,
    IStatProvider? InstigatorStats = null,
    Vector3? ImpactDirection = null);
