namespace Jmodot.Core.Interaction;

using Godot;
using Jmodot.Core.Combat;

/// <summary>
/// One forceful action asking a host to shake riders off. A runtime value (plain record, not a
/// <c>[GlobalClass]</c> resource — mirrors <see cref="ReleasePayload"/>): the melee state or cast
/// pipeline constructs it, the host resolves it, and nothing downstream couples to the caller.
/// </summary>
/// <param name="Force">Force available to spend against rider grip. Spent weakest-remaining-grip first; whatever is left after the last shed is discarded, never carried.</param>
/// <param name="DamagePayload">Damage effects applied to every rider in the scope's damage set, through that rider's hurtbox. Carries damage only — the fling impulse is derived from force spent, not from authored knockback. Null for a shed that only pushes.</param>
/// <param name="Scope">Which riders the payload reaches. Never affects how force is spent.</param>
/// <param name="OriginPosition">World-space origin of the action, used to aim each shed rider's fling away from it.</param>
public record ShedRequest(
    float Force,
    IAttackPayload? DamagePayload,
    ShedDamageScope Scope,
    Vector3 OriginPosition);
