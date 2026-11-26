namespace Jmodot.Core.Actors;

using Jmodot.Implementation.Actors;

/// <summary>
/// Capability an entity component exposes to consume a force-driven impact before any
/// <see cref="ImpactDetector.Impacted"/> subscriber sees it. The detector offers every impact to
/// its one absorber slot (<see cref="ImpactDetector.SetAbsorber"/>); a consumed impact is still
/// logged as an <c>ImpactResult</c> but is never raised, so a damage applier and an absorber never
/// race on subscriber order. Generic — an ice shell, a shield or a phased state all implement it
/// the same way.
/// </summary>
public interface IImpactAbsorber
{
    /// <summary>Return true to consume this impact. A consumed impact reaches no <c>Impacted</c> subscriber.</summary>
    bool TryAbsorb(in ImpactInfo info);
}
