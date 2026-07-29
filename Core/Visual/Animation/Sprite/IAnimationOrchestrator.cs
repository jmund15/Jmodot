namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;

/// <summary>
/// Core abstraction for animation orchestration.
/// Coordinates a base animation name with direction suffixes to produce final animation names.
/// Extends IAnimComponent with direction and base-name awareness.
/// </summary>
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
    /// Checks if an animation exists by base name (will check with current direction suffix applied).
    /// </summary>
    bool HasAnimationBase(StringName baseName);

    /// <summary>
    /// Returns total duration of an animation by base name (with current direction suffix applied).
    /// </summary>
    float GetAnimationLengthBase(StringName baseName);
}
