namespace Jmodot.Implementation.AI.Animation;

using Godot;

/// <summary>
/// Carries one claim tier's debounce state between frames. A record struct so the resolver stays
/// stateless per tier and the whole decision is pinnable without a scene.
/// </summary>
/// <param name="Held">The claim currently being held, or null when the tier is released.</param>
/// <param name="MissFrames">Consecutive frames <see cref="Held" /> has gone unclaimed.</param>
public readonly record struct ClaimHold(StringName? Held, int MissFrames);

/// <summary>
/// The pure decisions behind entity locomotion animation: whether a flat speed reads as moving,
/// which scope owns the base clip, and how long a momentarily-absent claim keeps holding it. Kept
/// free of node access so each is testable in isolation from
/// <see cref="AnimationClaimResolver" />'s scene-tree walks. Whether a resolved clip actually needs
/// pushing is the animator's own decision — see
/// <see cref="Core.Visual.Animation.Sprite.IAnimationOrchestrator.StartAnimIfChanged" />.
/// </summary>
public static class LocomotionAnimResolver
{
    /// <summary>
    /// Hysteresis band over squared flat speed. A single threshold chatters while an entity hovers
    /// at the boundary, and every flip costs a clip restart, so entering movement and leaving it
    /// use separate thresholds and the previous state carries through the band between them.
    /// </summary>
    public static bool ResolveIsMoving(float flatSpeedSq, bool wasMoving, float enterSq, float exitSq)
    {
        if (wasMoving)
        {
            return flatSpeedSq > exitSq;
        }
        return flatSpeedSq >= enterSq;
    }

    /// <summary>
    /// Innermost-wins precedence over base-clip claims: task ⊂ state ⊂ entity. Returns null when
    /// nothing claims and no locomotion clip is authored, which the resolver reads as "leave the
    /// animator alone".
    /// </summary>
    public static StringName? ResolveBaseAnim(StringName? taskClaim, StringName? stateClaim, StringName? locomotion)
    {
        if (IsClaim(taskClaim))
        {
            return taskClaim;
        }
        if (IsClaim(stateClaim))
        {
            return stateClaim;
        }
        return IsClaim(locomotion) ? locomotion : null;
    }

    /// <summary>
    /// Advances one claim tier by a frame. A live claim takes effect immediately — including a
    /// switch to a different claim — while a dropout holds the previous claim for
    /// <paramref name="graceFrames" /> frames before releasing. The grace exists because a
    /// composite BT task legitimately reports no running child for a single tick at a boundary,
    /// and an A→null→A flap would restart the claimant's clip.
    /// </summary>
    /// <param name="graceFrames">
    /// Consecutive unclaimed frames tolerated before release. A structural property of the tick
    /// boundary being debounced, not an entity tunable — it is a parameter so the boundary is
    /// pinnable without a scene.
    /// </param>
    public static ClaimHold AdvanceClaim(ClaimHold prev, StringName? currentClaim, int graceFrames)
    {
        if (IsClaim(currentClaim))
        {
            return new ClaimHold(currentClaim, 0);
        }
        if (!IsClaim(prev.Held))
        {
            return new ClaimHold(null, 0);
        }

        var missFrames = prev.MissFrames + 1;
        return missFrames > graceFrames ? new ClaimHold(null, 0) : new ClaimHold(prev.Held, missFrames);
    }

    private static bool IsClaim(StringName? name)
    {
        return name != null && !name.IsEmpty;
    }
}
