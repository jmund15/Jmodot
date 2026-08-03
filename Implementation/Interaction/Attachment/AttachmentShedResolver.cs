namespace Jmodot.Implementation.Interaction.Attachment;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.Interaction;

/// <summary>
/// Decides which riders a forceful action shakes off a host. Pure: no node access, no randomness,
/// no mutation — the host applies the returned <see cref="ShedPlan"/>.
/// </summary>
public static class AttachmentShedResolver
{
    /// <summary>
    /// Spend <paramref name="force"/> against <paramref name="records"/>, weakest remaining grip
    /// first with ties broken by attach sequence, and report the result for every rider.
    /// Force that outlives the roster is discarded rather than carried, and
    /// <paramref name="scope"/> selects the damage set without ever changing how force is spent.
    /// </summary>
    public static ShedPlan Resolve(
        IReadOnlyList<AttachmentRecord> records, float force, ShedDamageScope scope)
    {
        if (records == null || records.Count == 0) { return ShedPlan.Empty; }

        var ordered = records
            .OrderBy(r => r.RemainingGrip)
            .ThenBy(r => r.AttachSequence)
            .ToList();

        var damagesEveryone = scope == ShedDamageScope.AllAttached;
        var available = Mathf.Max(force, 0f);
        var outcomes = new List<ShedOutcome>(ordered.Count);

        foreach (var record in ordered)
        {
            var grip = Mathf.Max(record.RemainingGrip, 0f);
            var spent = Mathf.Min(available, grip);

            // Zero force must shed nothing, so a rider is only shed by force that actually reached it.
            var wasShed = available > 0f && grip - spent <= 0f;

            outcomes.Add(new ShedOutcome(
                record,
                spent,
                grip - spent,
                wasShed,
                damagesEveryone || wasShed));

            available -= spent;
        }

        return new ShedPlan(outcomes);
    }
}
