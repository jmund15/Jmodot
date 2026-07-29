namespace Jmodot.Core.Interaction;

/// <summary>
/// What a single shed resolution decided for one attached rider. The resolver produces these; the
/// host applies them (write back <see cref="RemainingGrip"/>, hurt the damaged, fling the shed).
/// </summary>
/// <param name="Record">The record as it stood when the shed was resolved.</param>
/// <param name="ForceSpent">Force this rider absorbed. Scales its fling impulse — zero for a rider the force never reached.</param>
/// <param name="RemainingGrip">Grip left after <see cref="ForceSpent"/> was taken off. Zero exactly when the rider was shed.</param>
/// <param name="WasShed">Whether the rider's grip was exhausted and it leaves the host.</param>
/// <param name="TakesDamage">Whether the request's payload reaches this rider, per <see cref="ShedDamageScope"/>.</param>
public readonly record struct ShedOutcome(
    AttachmentRecord Record,
    float ForceSpent,
    float RemainingGrip,
    bool WasShed,
    bool TakesDamage);
