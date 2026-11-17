namespace Jmodot.Implementation.Visual.WorldUi;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Pure placement math for world-space UI that brackets measured art.
/// </summary>
/// <remarks>
/// Every length passed in is a SCREEN quantity: the art extent <c>EntityVisualBounds3D</c> reports is
/// the size the art reads as (iso-tilted and billboarded art both face the screen), and element
/// heights are billboarded. Only the returned stack centres are world-Y offsets, and the one
/// conversion between the two is division by the screen-up factor.
/// </remarks>
public static class WorldUiLayout
{
    /// <summary>The steepest camera pitch below horizontal, in degrees, that world UI honours unclamped.</summary>
    public const float MaxSupportedTiltDeg = 80f;

    /// <summary>
    /// World-Y centres of elements stacked away from the art, nearest first, each separated from
    /// the art edge and from the previous element by <paramref name="padding"/> in screen space.
    /// </summary>
    /// <param name="artCentreY">World-Y of the art's centre.</param>
    /// <param name="artHalfHeight">Half the art's screen height. It is divided by <paramref name="screenUp"/>
    /// together with the padding: offsetting from a world-Y art edge instead under-clears tilted art by
    /// the screen-up factor.</param>
    /// <param name="padding">Screen gap, in metres, between the art and each element.</param>
    /// <param name="screenUp">From <see cref="ScreenUpFactor"/>; must be in (0, 1].</param>
    /// <param name="heights">Each element's screen height, nearest first.</param>
    /// <param name="direction"><c>+1</c> stacks above the art, <c>-1</c> below it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is neither +1 nor -1, or
    /// <paramref name="screenUp"/> is outside (0, 1] or NaN.</exception>
    public static float[] StackCentres(
        float artCentreY, float artHalfHeight, float padding, float screenUp, IReadOnlyList<float> heights, int direction)
    {
        if (direction is not (1 or -1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Stack direction is +1 (above) or -1 (below).");
        }

        if (!(screenUp > 0f && screenUp <= 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(screenUp), screenUp, "Screen-up factor is in (0, 1]; pass it through ScreenUpFactor.");
        }

        var centres = new float[heights.Count];
        float cursor = artHalfHeight + padding;
        for (int i = 0; i < heights.Count; i++)
        {
            centres[i] = artCentreY + (direction * (cursor + (heights[i] * 0.5f)) / screenUp);
            cursor += heights[i] + padding;
        }

        return centres;
    }

    /// <summary>Centres of items laid edge to edge, each as wide as its own width, the row centred on <paramref name="centreX"/>.</summary>
    public static float[] RowCentres(IReadOnlyList<float> widths, float centreX)
    {
        float total = 0f;
        foreach (float width in widths)
        {
            total += width;
        }

        var centres = new float[widths.Count];
        float cursor = centreX - (total * 0.5f);
        for (int i = 0; i < widths.Count; i++)
        {
            centres[i] = cursor + (widths[i] * 0.5f);
            cursor += widths[i];
        }

        return centres;
    }

    /// <summary>
    /// The share of a world-Y offset that survives as screen-up separation: the camera basis'
    /// <c>Y.y</c>, which is cos(pitch). Its magnitude is used, and a non-finite input is 1 (no foreshortening).
    /// </summary>
    /// <remarks>
    /// The result is clamped to [cos(<paramref name="maxTiltDeg"/>), 1]. That floor is the screen-up factor at
    /// the steepest pitch the caller honours. A camera looking straight down projects no world-Y separation,
    /// and dividing by it would fling a stack to infinity. A non-finite <paramref name="maxTiltDeg"/> means
    /// <see cref="MaxSupportedTiltDeg"/>.
    /// </remarks>
    public static float ScreenUpFactor(float cameraBasisYY, float maxTiltDeg = MaxSupportedTiltDeg)
    {
        float factor = Mathf.Abs(cameraBasisYY);
        if (!float.IsFinite(factor))
        {
            return 1f;
        }

        float tiltDeg = float.IsFinite(maxTiltDeg) ? Mathf.Clamp(maxTiltDeg, 0f, 90f) : MaxSupportedTiltDeg;
        float floor = Mathf.Cos(Mathf.DegToRad(tiltDeg));
        return Mathf.Clamp(factor, floor, 1f);
    }
}
