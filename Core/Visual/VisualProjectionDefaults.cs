namespace Jmodot.Core.Visual;

/// <summary>
/// Project-wide 2.5D projection constants for tilted-camera sprite facing. A consuming game's
/// active camera pushes its derived values here at startup (framework-agnostic seam; mirrors
/// <c>CombatFactoryDefaults</c> / <c>CollisionDefaults</c> — Jmodot never learns what a camera is).
/// </summary>
public static class VisualProjectionDefaults
{
    private static float _depthForeshorten = 1f;

    /// <summary>
    /// Screen foreshortening of world depth (Z) relative to world horizontal (X):
    /// = sin(camera pitch from horizontal). <c>1.0</c> = top-down / no foreshortening (the identity
    /// default, so an unconfigured consumer keeps pre-seam behavior — no silent regression).
    /// </summary>
    public static float DepthForeshorten
    {
        get => _depthForeshorten;
        set
        {
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

    /// <summary>Restore the identity default. Call in test teardown to avoid cross-suite leakage.</summary>
    public static void Reset()
    {
        _depthForeshorten = 1f;
        IsPublished = false;
    }
}
