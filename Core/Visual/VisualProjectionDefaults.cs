namespace Jmodot.Core.Visual;

using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Project-wide 2.5D projection constants for tilted-camera sprite facing. A consuming game's
/// active camera pushes its derived values here at startup (framework-agnostic seam; mirrors
/// <c>CombatFactoryDefaults</c> / <c>CollisionDefaults</c> — Jmodot never learns what a camera is).
/// The active camera <see cref="Retract"/>s on exit, so a scene transition away from the publishing
/// camera restores the unpublished identity rather than leaving stale tilt live for the next scene.
/// </summary>
public static class VisualProjectionDefaults
{
    private static float _depthForeshorten = 1f;

    /// <summary>
    /// Screen foreshortening of world depth (Z) relative to world horizontal (X):
    /// = sin(camera pitch from horizontal). <c>1.0</c> = top-down / no foreshortening (the identity
    /// default, so an unconfigured consumer keeps pre-seam behavior — no silent regression).
    /// Writing this publishes; a write of a DIFFERENT value while already published warns, because
    /// that means two active cameras disagree and the seam is single-writer by contract.
    /// </summary>
    public static float DepthForeshorten
    {
        get => _depthForeshorten;
        set
        {
            if (IsPublished && !Mathf.IsEqualApprox(value, _depthForeshorten))
            {
                JmoLogger.Warning(nameof(VisualProjectionDefaults),
                    $"Projection foreshorten {_depthForeshorten:F4} is already published; a second " +
                    $"active camera is overwriting it with {value:F4}. Last writer wins — iso sprite " +
                    "facing follows the most recent publisher. Expected one active camera per scene.");
            }

            _depthForeshorten = value;
            IsPublished = true;
        }
    }

    /// <summary>
    /// Whether an active camera has actually published a projection. Consumers that OVERWRITE
    /// scene state (rather than merely reading a factor) must gate on this: the identity default
    /// is indistinguishable from a genuine top-down camera by value alone, and acting on it
    /// applies a 90° frame that lays sprite planes edge-on — invisible, with no error.
    /// </summary>
    public static bool IsPublished { get; private set; }

    /// <summary>
    /// Un-publish, restoring the identity default. The active camera calls this on exit (its
    /// <c>_ExitTree</c>) so a scene transition doesn't leave the departed camera's tilt live for a
    /// scene whose own camera hasn't published yet — the unpublished state re-gates consumers back
    /// onto their authored basis until the next camera publishes.
    /// </summary>
    public static void Retract()
    {
        _depthForeshorten = 1f;
        IsPublished = false;
    }

    /// <summary>Restore the identity default. Call in test teardown to avoid cross-suite leakage.</summary>
    public static void Reset() => Retract();
}
