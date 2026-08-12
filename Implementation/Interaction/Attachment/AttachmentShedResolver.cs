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

    /// <summary>
    /// Aim one shed rider's fling. Most-specific source first: the rider's own anchor relative to the
    /// action's origin, then <paramref name="impactDirection"/> — the direction the blow travelled,
    /// supplied by the attacker for riders whose anchor cannot imply one — then a stable last resort.
    /// </summary>
    public static Vector3 ResolveFlingDirection(Vector3 anchorWorld, Vector3 origin, Vector3? impactDirection)
    {
        // Flattened BEFORE the emptiness test, not after: knockback discards Y, so a rider sitting
        // almost directly above the origin has a long vector that collapses to nothing once flattened,
        // and a post-flatten check would hand it a normalized zero instead of the next candidate.
        var away = new Vector3(anchorWorld.X - origin.X, 0f, anchorWorld.Z - origin.Z);
        if (!away.IsZeroApprox()) { return away.Normalized(); }

        // Flattened for the same reason and judged by the same test: a purely vertical blow carries no
        // horizontal aim and must reach the fallback rather than normalize a zero vector.
        var impact = impactDirection ?? Vector3.Zero;
        var alongBlow = new Vector3(impact.X, 0f, impact.Z);
        if (!alongBlow.IsZeroApprox()) { return alongBlow.Normalized(); }

        // Back is a stable horizontal fallback; Up would resolve to no impulse at all.
        return Vector3.Back;
    }
}
