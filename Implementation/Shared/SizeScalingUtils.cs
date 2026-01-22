namespace Jmodot.Implementation.Shared;

using Godot;

/// <summary>
/// Pure utility functions for size/scale calculations.
/// Used by EntitySizeController for scaling entities (spells, wizards, etc.).
/// </summary>
public static class SizeScalingUtils
{
    /// <summary>
    /// Clamps the size multiplier between min and max bounds.
    /// </summary>
    /// <param name="size">The input size multiplier</param>
    /// <param name="min">Minimum allowed size</param>
    /// <param name="max">Maximum allowed size</param>
    /// <returns>The clamped size value</returns>
    public static float ClampSize(float size, float min, float max)
    {
        return Mathf.Clamp(size, min, max);
    }

    /// <summary>
    /// Calculates the new scale by multiplying base scale by size multiplier.
    /// </summary>
    /// <param name="baseScale">The original scale vector</param>
    /// <param name="multiplier">The size multiplier to apply</param>
    /// <returns>The scaled vector (baseScale * multiplier)</returns>
    public static Vector3 ApplyScale(Vector3 baseScale, float multiplier)
    {
        return baseScale * multiplier;
    }
}
