namespace Jmodot.Core.Math;

using System;
using Godot;

/// <summary>
/// Generates <see cref="Curve"/> resources from <see cref="CurveShapePreset"/> values.
/// All curves map X (0→1) to Y (0→1) using standard easing functions.
/// Uses 2-point Hermite interpolation with exact mathematical tangents for smooth shapes.
/// When <paramref name="invert"/> is true, generates the X-reflected curve y=f(1-x).
/// </summary>
public static class CurveShapeFactory
{
    /// <summary>tan(60°) ≈ 1.732 — the slope used for ease-in/ease-out tangents.</summary>
    private const float Tan60 = 1.732f;

    /// <summary>
    /// Creates a Curve for the given preset, optionally inverted (X-reflected).
    /// Throws for <see cref="CurveShapePreset.Custom"/> (caller provides their own curve).
    /// </summary>
    public static Curve Create(CurveShapePreset preset, bool invert = false)
    {
        var curve = preset switch
        {
            CurveShapePreset.Constant => CreateConstant(),
            CurveShapePreset.Linear => CreateLinear(),
            CurveShapePreset.EaseIn => CreateEaseIn(),
            CurveShapePreset.EaseOut => CreateEaseOut(),
            CurveShapePreset.PlateauDrop => CreatePlateauDrop(),
            CurveShapePreset.EaseInOut => CreateEaseInOut(),
            CurveShapePreset.Custom => throw new ArgumentException(
                "Custom preset requires a manually-assigned Curve.", nameof(preset)),
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };

        return invert ? InvertCurve(curve) : curve;
    }

    /// <summary>
    /// Flat 1.0 — constant value at all inputs.
    /// </summary>
    private static Curve CreateConstant()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f), 0f, 0f);
        curve.AddPoint(new Vector2(1f, 1f), 0f, 0f);
        return curve;
    }

    /// <summary>
    /// Straight diagonal from (0,0) to (1,1) — linear ramp.
    /// At x=0: y=0, dy/dx=1. At x=1: y=1, dy/dx=1.
    /// </summary>
    private static Curve CreateLinear()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 0f), 0f, 1f);
        curve.AddPoint(new Vector2(1f, 1f), 1f, 0f);
        return curve;
    }

    /// <summary>
    /// Ease-in: slow start, accelerating toward end.
    /// At x=0: y=0, dy/dx=0. At x=1: y=1, dy/dx=tan(60°).
    /// </summary>
    private static Curve CreateEaseIn()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 0f), 0f, 0f);
        curve.AddPoint(new Vector2(1f, 1f), Tan60, 0f);
        return curve;
    }

    /// <summary>
    /// Ease-out: fast start, decelerating toward end.
    /// At x=0: y=0, dy/dx=tan(60°). At x=1: y=1, dy/dx=0.
    /// </summary>
    private static Curve CreateEaseOut()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 0f), 0f, Tan60);
        curve.AddPoint(new Vector2(1f, 1f), 0f, 0f);
        return curve;
    }

    /// <summary>
    /// Piecewise plateau-drop: holds at 1.0, smooth S-curve drop, then flat at 0.
    /// Uses zero tangents at all points — Hermite interpolation creates a smooth transition.
    /// </summary>
    private static Curve CreatePlateauDrop()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f), 0f, 0f);
        curve.AddPoint(new Vector2(0.3f, 1f), 0f, 0f);
        curve.AddPoint(new Vector2(0.5f, 0f), 0f, 0f);
        curve.AddPoint(new Vector2(1f, 0f), 0f, 0f);
        return curve;
    }

    /// <summary>
    /// Smoothstep ease-in-out: y = 3x²-2x³.
    /// At x=0: y=0, dy/dx=0. At x=1: y=1, dy/dx=0.
    /// </summary>
    private static Curve CreateEaseInOut()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 0f), 0f, 0f);
        curve.AddPoint(new Vector2(1f, 1f), 0f, 0f);
        return curve;
    }

    /// <summary>
    /// Creates the X-reflected version of a curve: y = f(1-x).
    /// Each point (x, y) becomes (1-x, y) with tangents swapped and negated.
    /// Points are emitted in ascending X order.
    /// </summary>
    private static Curve InvertCurve(Curve original)
    {
        var inverted = new Curve();

        // Iterate in reverse so output points are in ascending X order
        for (int i = original.PointCount - 1; i >= 0; i--)
        {
            var pos = original.GetPointPosition(i);
            float lt = original.GetPointLeftTangent(i);
            float rt = original.GetPointRightTangent(i);

            inverted.AddPoint(
                new Vector2(1f - pos.X, pos.Y),
                -rt, // old right tangent → new left tangent (negated)
                -lt  // old left tangent → new right tangent (negated)
            );
        }

        return inverted;
    }
}
