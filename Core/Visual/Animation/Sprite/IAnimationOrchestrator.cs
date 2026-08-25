namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;

/// <summary>
/// Core abstraction for animation orchestration.
/// Coordinates a base animation name with direction suffixes to produce final animation names.
/// Extends IAnimComponent with direction and base-name awareness.
/// </summary>
/// <remarks>
/// An orchestrator holds a STANDING request — the base name a caller last asked for — and re-resolves
/// it whenever the facing changes, which is how a clip follows a turning body. Ending that clip is
/// therefore two different acts, and callers must pick deliberately:
/// <list type="bullet">
/// <item><see cref="IAnimComponent.StopAnim" /> means "I no longer want this visual": it RETIRES the
/// standing request, so a later facing change resolves nothing and the clip stays down.</item>
/// <item>A clip that ends on its OWN keeps the request, so a later facing change re-resolves it for
/// the new facing and holds its completed pose.</item>
/// </list>
/// Without the first half, a transient overlay cleared by its owner comes straight back on the body's
/// next turn — seeked to its final frame, because nothing was playing.
/// </remarks>
public interface IAnimationOrchestrator : IAnimComponent
{
    /// <summary>
    /// The current base animation name (e.g., "run", "idle") before direction suffix is applied.
    /// </summary>
    StringName BaseAnimName { get; }

    /// <summary>
    /// The current animation direction as a world-space vector.
    /// </summary>
    Vector3 CurrentAnimationDirection { get; }

    /// <summary>
    /// Updates the animation direction. Triggers a smooth update to preserve animation time.
    /// </summary>
    void SetDirection(Vector3 direction);

    /// <summary>
    /// Pushes <paramref name="baseName" /> onto the base layer only when it would actually change what
    /// is playing. The correct way to drive the base layer from any caller that does not own it
    /// exclusively.
    /// </summary>
    /// <remarks>
    /// Both halves of the guard are load-bearing. Comparing against <see cref="BaseAnimName" /> rather
    /// than a caller-side mirror matters because any other caller can change the base underneath you —
    /// a fire-and-forget one-shot reports completion immediately and is invisible to a caller tracking
    /// its own last-pushed value, after which that stale mirror suppresses the legitimate reclaim and
    /// strands the one-shot as the permanent base. Checking <see cref="IAnimComponent.IsPlaying" />
    /// matters because <see cref="BaseAnimName" /> carries its field default before anything has ever
    /// played: name-equality alone would suppress the very first push, so nothing emits AnimStarted and
    /// any visibility coordinator gated on that event leaves the body hidden. It also re-drives the base
    /// after a non-looping clip ends and leaves no current animation.
    /// </remarks>
    void StartAnimIfChanged(StringName baseName)
    {
        if (this.IsPlaying() && baseName == this.BaseAnimName) { return; }
        this.StartAnim(baseName);
    }

    /// <summary>
    /// Starts <paramref name="baseName" /> time-scaled so the clip completes in exactly
    /// <paramref name="seconds" />. For a telegraph whose length is dictated by a gameplay window —
    /// a charge step, a wind-up, a channel — rather than by its authored frame rate, so what the
    /// player watches and what the mechanic is counting stay in step.
    /// </summary>
    /// <remarks>
    /// <para>PRECONDITION: the target animator's speed register has no per-frame writer. This is an
    /// ABSOLUTE fit, and <see cref="IAnimComponent.SetSpeedScale" /> is a single shared register —
    /// on a rig where a velocity- or time-driven speed profile also writes it every frame, the fit is
    /// overwritten the next frame with no signal. Use it on a dedicated overlay orchestrator, or on a
    /// rig whose profile is inactive for the duration.</para>
    /// <para>The scale lands on the animator and PERSISTS past this clip, exactly as
    /// <see cref="IAnimComponent.SetSpeedScale" /> does; a caller that later wants authored tempo
    /// restores it. Fitted once, against the clip this call actually resolved: a direction change
    /// mid-clip keeps the scale, so directional variants of one base name are assumed to share a
    /// length (every clip set authored from one sheet row-pair does).</para>
    /// <para>Any <paramref name="seconds" /> that is not finite and positive — zero, negative, NaN,
    /// or infinity — falls back to authored tempo, as does a base name that resolves to nothing.
    /// Infinity is the one that reads safe and is not: it divides to a scale of ZERO, which pins the
    /// animator on its first frame for that clip and every clip after it.</para>
    /// </remarks>
    void StartAnimOverDuration(StringName baseName, float seconds)
    {
        this.StartAnim(baseName);
        var length = this.IsPlaying() ? this.GetCurrAnimationLength() : 0f;
        var fits = float.IsFinite(seconds) && seconds > 0f && length > 0f;
        this.SetSpeedScale(fits ? length / seconds : 1f);
    }

    /// <summary>
    /// Checks if an animation exists by base name (will check with current direction suffix applied).
    /// </summary>
    bool HasAnimationBase(StringName baseName);

    /// <summary>
    /// Returns total duration of an animation by base name (with current direction suffix applied).
    /// </summary>
    float GetAnimationLengthBase(StringName baseName);
}
