namespace Jmodot.Core.Shared;

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

    public static IEnumerable<T> GetEnumValues<T>()
    {
        return Enum.GetValues(typeof(T)).Cast<T>();
    }
}
