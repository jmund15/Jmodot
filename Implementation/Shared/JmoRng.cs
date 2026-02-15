namespace Jmodot.Implementation.Shared;

using System;

/// <summary>
/// Centralized RNG utilities. All game randomness flows through <see cref="Rnd"/>
/// to ensure testability outside the Godot runtime.
/// </summary>
public static class JmoRng
{
    public static readonly Random Rnd = new(Guid.NewGuid().GetHashCode());

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
}
