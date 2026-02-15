namespace Jmodot.Implementation.Shared;

using System;
using System.Collections.Generic;
using System.Linq;

public static class MiscUtils
{
    public static readonly Random Rnd = new(Guid.NewGuid().GetHashCode());

    public static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
    {
        return outMin + (outMax - outMin) * ((value - inMin) / (inMax - inMin));
    }

    public static float GetRndInRange(float min, float max)
    {
        var normF = Rnd.NextSingle();
        return normF * (max - min) + min;
    }

    /// <summary>Returns a random int in [min, max] (inclusive both ends). Matches GD.RandRange(int, int) behavior.</summary>
    public static int GetRndInRange(int min, int max)
    {
        return Rnd.Next(min, max + 1);
    }

    /// <summary>Returns +1f or -1f randomly.</summary>
    public static float GetRndSign()
    {
        return Rnd.Next(2) == 0 ? 1f : -1f;
    }

    /// <summary>Returns a random int in [0, max). For array indexing.</summary>
    public static int GetRndInt(int max)
    {
        return Rnd.Next(max);
    }

    /// <summary>Returns a random float in [0, 1). Replaces GD.Randf() and Random.Shared.NextSingle().</summary>
    public static float GetRndFloat()
    {
        return Rnd.NextSingle();
    }

    public static Vector2 GetRndVector2()
    {
        var x = GetRndInRange(-1.0f, 1.0f);
        var y = GetRndInRange(-1.0f, 1.0f);
        return new Vector2(x, y).Normalized();
    }

    public static Vector3 GetRndVector3()
    {
        var x = GetRndInRange(-1.0f, 1.0f);
        var y = GetRndInRange(-1.0f, 1.0f);
        var z = GetRndInRange(-1.0f, 1.0f);
        return new Vector3(x, y, z).Normalized();
    }

    public static Vector3 GetRndVector3PosY()
    {
        var x = GetRndInRange(-1.0f, 1.0f);
        var y = GetRndInRange(0f, 1.0f);
        var z = GetRndInRange(-1.0f, 1.0f);
        return new Vector3(x, y, z).Normalized();
    }

    public static Vector3 GetRndVector3ZeroY()
    {
        var rnd2 = GetRndVector2();
        return new Vector3(rnd2.X, 0f, rnd2.Y).Normalized();
    }

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

    public static IEnumerable<T> GetEnumValues<T>()
    {
        return Enum.GetValues(typeof(T)).Cast<T>();
    }

    public static void DisableProcessing(Node node)
    {
        node.SetProcess(false);
        node.SetPhysicsProcess(false);

        if (node is Node2D node2D)
        {
            node2D.SetProcessInput(false);
            node2D.SetProcessUnhandledInput(false);
        }
        else if (node is Node3D node3D)
        {
            node3D.SetProcessInput(false);
            node3D.SetProcessUnhandledInput(false);
        }
    }
}
