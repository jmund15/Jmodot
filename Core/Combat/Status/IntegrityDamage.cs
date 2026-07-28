namespace Jmodot.Core.Combat.Status;

/// <summary>
/// Outcome of chipping a decaying-integrity status pool.
/// <see cref="Remaining"/> is signed and goes negative on overkill, so a caller can derive the
/// portion of the blow the pool failed to absorb rather than only learning that it broke.
/// </summary>
public readonly record struct IntegrityDamageResult(bool Depleted, float Remaining);

/// <summary>
/// What chipped an integrity pool. Lets a consumer weigh sources differently without the runner
/// having to know why any of them happened.
/// </summary>
public enum IntegrityDamageSource { Hit, WallSlam }
