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
    /// Checks if an animation exists by base name (will check with current direction suffix applied).
    /// </summary>
    bool HasAnimationBase(StringName baseName);

    /// <summary>
    /// Returns total duration of an animation by base name (with current direction suffix applied).
    /// </summary>
    float GetAnimationLengthBase(StringName baseName);
}
