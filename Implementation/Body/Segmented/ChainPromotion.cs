namespace Jmodot.Implementation.Body.Segmented;

using System;
using System.Collections.Generic;
using Godot;
using Core.AI.BB;
using Implementation.AI.BB;
using Implementation.Health;
using Implementation.Shared;
using Implementation.Shared.GodotExceptions;

/// <summary>
/// The ordered steps that turn a severed run of units into a body of its own. A collaborator of
/// <see cref="SegmentedBodyComponent3D"/> rather than a rung of it: following a path and spawning a
/// seeded entity are different concerns, and separating them is what leaves the arithmetic and the
/// ordering testable without a scene.
/// </summary>
/// <remarks>
/// Two orderings are load-bearing and neither is recoverable if broken. The new body's pose is
/// written as a LOCAL transform before it is parented, because a global write outside the tree is
/// dropped; and the severed tail is adopted before parenting too, because a chain that already
/// holds units when it readies skips its own initial spawn — adopting afterwards would leave the
/// progeny wearing a fresh random body on top of the one it inherited.
/// </remarks>
internal static class ChainPromotion
{

    /// <summary>
    /// The seed a progeny enters the world with: a child of its parent's own stream, distinguished
    /// by <paramref name="ordinal"/> so several fragments promoting in one frame diverge from each
    /// other as well as from the parent.
    /// </summary>
    internal static int DeriveProgenySeed(int parentSeed, int ordinal)
        => SeedManager.DeriveChild(parentSeed, SeedKinds.Progeny, ordinal);

    /// <summary>
    /// The health a promoted body starts on: the fraction its front unit was carrying, applied to
    /// the new body's own pool, which is a different size. Null means do not seed at all.
    /// </summary>
    /// <returns>
    /// Null when the new body has no resolvable pool — seeding against it would spawn a progeny at
    /// zero health; a full pool when the SOURCE fraction is unresolvable, which is the generous
    /// degradation; the fraction otherwise. Both degraded paths warn.
    /// </returns>
    internal static float? ResolveSeedHealth(object context, float sourceCurrent, float sourceMax, float targetMax)
    {
        if (!(targetMax > 0f) || !float.IsFinite(targetMax))
        {
            JmoLogger.Warning(context,
                "[SegmentedBody] A promoted body resolved no max health, so its health is left as spawned rather than multiplied to zero.");
            return null;
        }

        if (!(sourceMax > 0f) || !float.IsFinite(sourceMax) || !float.IsFinite(sourceCurrent))
        {
            JmoLogger.Warning(context,
                "[SegmentedBody] A promoted unit carried no resolvable health fraction, so the body it becomes starts full.");
            return targetMax;
        }

        return targetMax * (sourceCurrent / sourceMax);
    }

    /// <summary>
    /// Instantiates <paramref name="headRoot"/>'s own scene for <paramref name="fragment"/>, seeds
    /// it, hands it the fragment behind its front unit, parents it beside the body it split from,
    /// and frees the front unit it replaced.
    /// </summary>
    /// <param name="fragment">
    /// The severed run, front first, already detached from <paramref name="source"/>. Its front unit
    /// becomes the new head and is FREED, never killed — promotion is a transformation, and drops in
    /// a fight must equal units actually killed.
    /// </param>
    /// <returns>The new body's root, already in the tree and initialized.</returns>
    /// <exception cref="NodeConfigurationException">
    /// The head did not come from a saved scene, or that scene carries no chain of its own.
    /// </exception>
    internal static Node3D Promote(
        SegmentedBodyComponent3D source,
        Node3D headRoot,
        IReadOnlyList<BodySegment3D> fragment,
        Node container,
        int parentSeed,
        int ordinal)
    {
        var scenePath = headRoot.SceneFilePath;
        if (string.IsNullOrEmpty(scenePath))
        {
            throw new NodeConfigurationException(
                "A segmented body's head must come from a saved scene — promotion re-instantiates that scene, which is also where the progeny's identity rides.",
                headRoot);
        }

        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            throw new NodeConfigurationException(
                $"The head's scene path '{scenePath}' did not load as a PackedScene — promotion cannot re-instantiate the head.",
                headRoot);
        }

        var progeny = scene.Instantiate<Node3D>();
        var front = fragment[0];

        progeny.Transform = container is Node3D anchor
            ? anchor.GlobalTransform.AffineInverse() * front.GlobalTransform
            : front.GlobalTransform;

        progeny.TryGetFirstChildOfInterface<IBlackboard>(out var bb);
        bb?.Set(BBDataSig.EntitySeed, DeriveProgenySeed(parentSeed, ordinal));

        if (!progeny.TryGetFirstChildOfType<SegmentedBodyComponent3D>(out var chain) || chain == null)
        {
            progeny.Free();
            throw new NodeConfigurationException(
                "A segmented body's head scene must carry a SegmentedBodyComponent3D — a promoted fragment has nothing to ride otherwise.",
                headRoot);
        }

        var tail = new List<BodySegment3D>(Math.Max(fragment.Count - 1, 0));
        for (var i = 1; i < fragment.Count; i++) { tail.Add(fragment[i]); }
        chain.AdoptSegments(tail);

        container.AddChild(progeny);

        // Only now is the new pool resolvable: the root's _Ready ran its whole bootstrap inside AddChild.
        if (bb != null && bb.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health) && health != null)
        {
            var seeded = ResolveSeedHealth(health, front.Health.CurrentHealth, front.Health.MaxHealth, health.MaxHealth);
            if (seeded.HasValue) { health.SetCurrentHealth(seeded.Value, source); }
        }

        var frontRoot = front.Owner ?? (Node)front;
        if (GodotObject.IsInstanceValid(frontRoot) && !frontRoot.IsQueuedForDeletion())
        {
            frontRoot.QueueFree();
        }

        return progeny;
    }
}
