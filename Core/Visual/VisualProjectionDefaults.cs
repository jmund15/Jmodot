namespace Jmodot.Core.Visual;

/// <summary>
/// Project-wide 2.5D projection constants for tilted-camera sprite facing. A consuming game's
/// active camera pushes its derived values here at startup (framework-agnostic seam; mirrors
/// <c>CombatFactoryDefaults</c> / <c>CollisionDefaults</c> — Jmodot never learns what a camera is).
/// </summary>
public static class VisualProjectionDefaults
{
    /// <summary>
    /// Screen foreshortening of world depth (Z) relative to world horizontal (X):
    /// = sin(camera pitch from horizontal). <c>1.0</c> = top-down / no foreshortening (the identity
    /// default, so an unconfigured consumer keeps pre-seam behavior — no silent regression).
    /// </summary>
    public static float DepthForeshorten = 1f;

    /// <summary>Restore the identity default. Call in test teardown to avoid cross-suite leakage.</summary>
    public static void Reset() => DepthForeshorten = 1f;
}
