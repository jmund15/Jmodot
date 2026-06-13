namespace Jmodot.Implementation.Shared;

using Godot;
using Core.AI.BB;
using AI.BB;

/// <summary>
/// Resolves a per-agent seeded <see cref="JmoRng"/> for an AI consumer from the agent's
/// <see cref="BBDataSig.EntitySeed"/> Blackboard slot. The returned stream is deterministic
/// for a given (entity seed, <paramref name="kind"/>) pair and statistically isolated per
/// kind (different kinds off the same entity seed never share a sequence).
/// <para>
/// When the slot is absent — the host game has not adopted the entity-seed scheme, or a
/// bind-order gap left the slot unset — this returns <see cref="JmoRng.UnseededByDesign"/>
/// and emits ONE <see cref="JmoLogger"/> warning via the caller's per-instance latch. It
/// never throws: other Jmodot games may legitimately not adopt the scheme, so a Jmodot AI
/// consumer must degrade gracefully rather than fail-fast (the PP-side EntityBootstrapper
/// owns the strict posture).
/// </para>
/// </summary>
public static class EntityRngResolver
{
    /// <param name="bb">The agent's Blackboard (may be null in degenerate setups).</param>
    /// <param name="kind">A pinned <c>SeedKinds</c> component-kind segment.</param>
    /// <param name="warnContext">The consuming Node/Resource, for the warning's source tag.</param>
    /// <param name="warned">Per-instance latch — set true after the first fallback warning so
    /// a missing-seed consumer warns once, not every evaluation.</param>
    public static JmoRng Resolve(IBlackboard? bb, string kind, GodotObject warnContext, ref bool warned)
    {
        if (bb != null && bb.TryGet<int>(BBDataSig.EntitySeed, out var entitySeed))
        {
            return JmoRng.FromRawStreamName(kind, entitySeed);
        }

        if (!warned)
        {
            JmoLogger.Warning(warnContext,
                $"[Lineage] No EntitySeed for stream '{kind}' — UnseededByDesign fallback (non-deterministic).");
            warned = true;
        }
        return JmoRng.UnseededByDesign();
    }
}
