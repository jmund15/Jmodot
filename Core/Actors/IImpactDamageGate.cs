namespace Jmodot.Core.Actors;

using Jmodot.Implementation.Actors;

/// <summary>
/// Capability query a sibling component exposes to veto force-driven impact damage for a
/// single <see cref="ImpactInfo"/>. <see cref="ForceImpactDamageApplier"/> resolves all
/// gate siblings at init and skips both damage and velocity-loss when any gate denies.
/// Generic — carries no project-specific semantics (a frozen-body component, an
/// invulnerability window, etc. all implement it the same way).
/// </summary>
public interface IImpactDamageGate
{
    /// <summary>Return false to veto impact damage (and its velocity-loss) for this impact.</summary>
    bool AllowImpactDamage(in ImpactInfo info);
}
