namespace Jmodot.Implementation.Shared;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure math and geometry utility functions.
/// </summary>
public static class JmoMath
{
    /// <summary>
    /// Maps a value from input range [inMin, inMax] to output range [outMin, outMax].
    /// Supports extrapolation (values outside input range map beyond output range).
    /// Returns <paramref name="outMin"/> when <paramref name="inMin"/> equals <paramref name="inMax"/>
    /// to avoid division by zero.
    /// </summary>
    public static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
    {
        if (inMax == inMin) { return outMin; }
        return outMin + (outMax - outMin) * ((value - inMin) / (inMax - inMin));
    }

    /// <summary>
    /// Evaluates a quadratic Bezier curve at parameter <paramref name="t"/> (0 to 1).
    /// P0 is the start, P1 is the control point, P2 is the end.
    /// </summary>
    public static Vector3 QuadraticBezier3D(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        Vector3 q0 = p0.Lerp(p1, t);
        Vector3 q1 = p1.Lerp(p2, t);
        Vector3 r = q0.Lerp(q1, t);
        return r;
    }

    /// <summary>
    /// Returns a point on a ring (circle) around a center point, oriented by the given basis.
    /// Useful for aim rings, orbital positioning, melee attack targeting, etc.
    /// </summary>
    /// <param name="center">The center point of the ring (e.g., pivot GlobalPosition)</param>
    /// <param name="basis">The orientation of the ring plane (e.g., pivot GlobalTransform.Basis)</param>
    /// <param name="direction">2D direction input mapped to XZ plane</param>
    /// <param name="radius">Distance from center to ring edge</param>
    /// <param name="defaultDirection">Fallback direction when input is zero (defaults to Vector2.Right)</param>
    public static Vector3 GetPointOnRing(
        Vector3 center,
        Basis basis,
        Vector2 direction,
        float radius,
        Vector2? defaultDirection = null)
    {
        var dir = direction.IsZeroApprox()
            ? (defaultDirection ?? Vector2.Right)
            : direction.Normalized();

        return center + (basis * new Vector3(dir.X, 0, dir.Y) * radius);
    }

    /// <summary>Returns all values of enum type <typeparamref name="T"/> in declaration order.</summary>
    public static IEnumerable<T> GetEnumValues<T>() where T : struct, Enum
    {
        return Enum.GetValues<T>();
    }

    /// <summary>
    /// Frame-rate-independent exponential decay smoothing.
    /// Smoothly moves <paramref name="current"/> toward <paramref name="target"/> at a rate
    /// controlled by <paramref name="speed"/>. Higher speed = faster convergence.
    /// Mathematically: Lerp(current, target, 1 - exp(-speed * delta)).
    /// </summary>
    /// <param name="current">Current value.</param>
    /// <param name="target">Target value to approach.</param>
    /// <param name="speed">Decay rate (1/s). Higher = faster convergence. 0 = no movement.</param>
    /// <param name="delta">Frame delta time in seconds.</param>
    public static float ExpDecay(float current, float target, float speed, float delta)
    {
        if (delta <= 0f) { return current; }
        if (speed <= 0f) { return current; }
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * delta));
    }

    /// <summary>
    /// Frame-rate-independent exponential decay smoothing for Vector2.
    /// </summary>
    public static Vector2 ExpDecay(Vector2 current, Vector2 target, float speed, float delta)
    {
        if (delta <= 0f) { return current; }
        if (speed <= 0f) { return current; }
        float t = 1f - Mathf.Exp(-speed * delta);
        return current.Lerp(target, t);
    }

    /// <summary>
    /// Frame-rate-independent exponential decay smoothing for Vector3.
    /// </summary>
    public static Vector3 ExpDecay(Vector3 current, Vector3 target, float speed, float delta)
    {
        if (delta <= 0f) { return current; }
        if (speed <= 0f) { return current; }
        float t = 1f - Mathf.Exp(-speed * delta);
        return current.Lerp(target, t);
    }
}
