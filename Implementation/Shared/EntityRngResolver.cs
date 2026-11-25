namespace Jmodot.Implementation.Shared;

using System.Collections.Generic;
using Godot;
using Core.AI.BB;
using AI.BB;

/// <summary>
/// Resolves a per-agent seeded <see cref="JmoRng"/> for an AI consumer from the agent's
/// <see cref="BBDataSig.EntitySeed"/> Blackboard slot. The returned stream is deterministic
/// for a given (entity seed, <c>kind</c>) pair and statistically isolated per
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
    /// <summary>
    /// The stream for a node-owned consumer: one per (entity seed, <paramref name="kind"/>, node position under
    /// <paramref name="agent"/>). Two same-kind nodes under one entity draw independent streams; the key never reads
    /// a node's Name. Use <see cref="ResolveEntityScoped"/> for a stream that is one per entity and kind.
    /// </summary>
    /// <param name="bb">The agent's Blackboard (may be null in degenerate setups).</param>
    /// <param name="kind">A pinned <see cref="SeedKinds"/> component-kind segment.</param>
    /// <param name="agent">The entity root the node sits under.</param>
    /// <param name="node">The consuming node; null or freed keys as "unbound".</param>
    /// <param name="warnContext">The consuming Node/Resource, for the warning's source tag.</param>
    /// <param name="warned">Per-instance latch — set true after the first fallback warning.</param>
    public static JmoRng Resolve(IBlackboard? bb, string kind, Node? agent, Node? node, GodotObject warnContext, ref bool warned)
        => ResolveEntityScoped(bb, $"{kind}:{NodeKey(agent, node)}", warnContext, ref warned);

    /// <summary>
    /// The stream path of a node under its agent, the key <see cref="Resolve(IBlackboard?, string, Node?, Node?, GodotObject, ref bool)"/>
    /// appends to the kind. Each level below the agent contributes <c>Class[index]</c>, where index is the node's
    /// position among same-class siblings, joined root-first with <c>/</c>. Names never enter the key, so an engine
    /// auto-name cannot change a stream.
    /// </summary>
    public static string NodeKey(Node? agent, Node? node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
        {
            return "unbound";
        }

        if (node == agent)
        {
            return ".";
        }

        var segments = new List<string>();
        for (Node? current = node; current != null && current != agent; current = current.GetParent())
        {
            segments.Add(PathSegment(current));
        }
        segments.Reverse();
        return string.Join("/", segments);
    }

    private static string PathSegment(Node node)
    {
        var index = 0;
        var parent = node.GetParent();
        if (parent != null)
        {
            var type = node.GetType();
            foreach (var sibling in parent.GetChildren())
            {
                if (ReferenceEquals(sibling, node))
                {
                    break;
                }
                if (sibling.GetType() == type)
                {
                    index++;
                }
            }
        }
        return $"{node.GetType().Name}[{index}]";
    }

    /// <summary>
    /// The stream for a consumer that is one per entity and kind (an entity singleton, not a node in the BT tree).
    /// Its callers: <c>CasterComponent</c>, <c>AnimationOrchestrator</c>, <c>PunchState</c>, <c>IngredientInventoryComponent</c>,
    /// <c>AttachmentHostComponent3D</c>, <c>AttachmentRiderComponent3D</c>, <c>MovementQuirkProcessor2D</c>,
    /// <c>MovementQuirkProcessor3D</c>, <c>WaypointGoal3D</c> and <c>CompositeConsideration</c>. A repeatable node
    /// must use <see cref="Resolve(IBlackboard?, string, Node?, Node?, GodotObject, ref bool)"/> instead.
    /// </summary>
    /// <param name="bb">The agent's Blackboard (may be null in degenerate setups).</param>
    /// <param name="kind">A pinned <see cref="SeedKinds"/> component-kind segment.</param>
    /// <param name="warnContext">The consuming Node/Resource, for the warning's source tag.</param>
    /// <param name="warned">Per-instance latch — set true after the first fallback warning so
    /// a missing-seed consumer warns once, not every evaluation.</param>
    public static JmoRng ResolveEntityScoped(IBlackboard? bb, string kind, GodotObject warnContext, ref bool warned)
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

    /// <summary>
    /// The agent's per-kind unit value in [0, 1) (<see cref="SeedManager.DeriveUnit"/>), for a consumer that needs one
    /// seeded phase or delay and not a stream. A missing <see cref="BBDataSig.EntitySeed"/> reads 0 and warns once
    /// through <paramref name="warned"/>.
    /// </summary>
    public static float ResolveUnit(IBlackboard? bb, string kind, GodotObject warnContext, ref bool warned)
    {
        if (bb != null && bb.TryGet<int>(BBDataSig.EntitySeed, out var entitySeed))
        {
            return SeedManager.DeriveUnit(entitySeed, kind);
        }

        if (!warned)
        {
            JmoLogger.Warning(warnContext,
                $"[Lineage] No EntitySeed for unit '{kind}' — reading 0 (unseeded).");
            warned = true;
        }
        return 0f;
    }
}
