namespace Jmodot.Core.Math;

/// <summary>
/// Standard easing presets for <see cref="CurveShape"/>.
/// Selecting a preset generates and populates the curve.
/// All preset curves output values in the 0→1 range.
/// Use <see cref="CurveShape.Invert"/> to flip the X-axis sampling direction.
/// </summary>
public enum CurveShapePreset
{
    /// <summary>Flat 1.0 curve — constant value at all inputs.</summary>
    Constant,

    /// <summary>Ease-in: slow start, accelerating to full speed.</summary>
    EaseIn,

    /// <summary>Ease-out: fast start, decelerating to rest.</summary>
    EaseOut,

    /// <summary>Piecewise: holds steady, sharp cliff drop, then flat. Useful for shield/barrier effects.</summary>
    PlateauDrop,

    /// <summary>Smoothstep ease-in-out (3x²-2x³): slow start and end, fast in middle.</summary>
    EaseInOut,

    /// <summary>Use the manually-assigned Curve resource.</summary>
    Custom,

    /// <summary>Straight diagonal from (0,0) to (1,1) — linear ramp.</summary>
    Linear
}
