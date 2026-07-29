namespace Jmodot.Core.Interaction;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The complete decision of one shed resolution: an outcome for every attached rider, in the order
/// force was spent (weakest remaining grip first, ties by attach sequence). Purely descriptive — the
/// host performs every mutation, so the resolver stays testable without a scene.
/// </summary>
/// <param name="Outcomes">One entry per rider on the roster, in force-spending order.</param>
public sealed record ShedPlan(IReadOnlyList<ShedOutcome> Outcomes)
{
    /// <summary>A resolution that touched nothing — an empty roster, or a request against no host.</summary>
    public static readonly ShedPlan Empty = new(Array.Empty<ShedOutcome>());

    /// <summary>Riders whose grip was exhausted, in the order the force reached them.</summary>
    public IEnumerable<ShedOutcome> Shed => this.Outcomes.Where(o => o.WasShed);

    /// <summary>Riders the request's payload reaches, per its <see cref="ShedDamageScope"/>.</summary>
    public IEnumerable<ShedOutcome> Damaged => this.Outcomes.Where(o => o.TakesDamage);

    /// <summary>Force actually absorbed by riders. Less than the request's force whenever the roster ran out first.</summary>
    public float TotalForceSpent => this.Outcomes.Sum(o => o.ForceSpent);
}
