namespace Jmodot.Implementation.Shared;

using System;

/// <summary>
/// Centralized RNG utilities. All game randomness flows through <see cref="Rnd"/>
/// to ensure testability outside the Godot runtime.
/// </summary>
public static class JmoRng
{
    public static readonly Random Rnd = new(Guid.NewGuid().GetHashCode());

    /// <summary>
    /// Returns a random float in [min, max). The max value is exclusive due to
    /// <see cref="Random.NextSingle"/> returning [0, 1). Contrast with the int
    /// overload which is [min, max] inclusive.
    /// </summary>
    public static float GetRndInRange(float min, float max)
    {
        if (min > max)
        {
            throw new ArgumentException($"min ({min}) must not be greater than max ({max}).");
        }

        var normF = Rnd.NextSingle();
        return normF * (max - min) + min;
    }

    /// <summary>Returns a random int in [min, max] (inclusive both ends). Matches GD.RandRange(int, int) behavior.</summary>
    public static int GetRndInRange(int min, int max)
    {
        if (max == int.MaxValue)
        {
            // Rnd.Next(min, max+1) would overflow. Use long arithmetic via NextInt64.
            return (int)Rnd.NextInt64(min, (long)max + 1);
        }

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

    private const int MaxAttempts = 10;

    /// <summary>Returns a normalized random 2D direction vector with components in [-1, 1).</summary>
    public static Vector2 GetRndVector2()
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            var vec = new Vector2(GetRndInRange(-1.0f, 1.0f), GetRndInRange(-1.0f, 1.0f));
            if (!vec.IsZeroApprox())
            {
                return vec.Normalized();
            }
        }

        return Vector2.Right;
    }

    /// <summary>Returns a normalized random 3D direction vector with components in [-1, 1).</summary>
    public static Vector3 GetRndVector3()
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            var vec = new Vector3(GetRndInRange(-1.0f, 1.0f), GetRndInRange(-1.0f, 1.0f), GetRndInRange(-1.0f, 1.0f));
            if (!vec.IsZeroApprox())
            {
                return vec.Normalized();
            }
        }

        return Vector3.Right;
    }

    /// <summary>Returns a normalized random 3D direction with positive Y (upward hemisphere). Y in [0, 1).</summary>
    public static Vector3 GetRndVector3PosY()
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            var vec = new Vector3(GetRndInRange(-1.0f, 1.0f), GetRndInRange(0f, 1.0f), GetRndInRange(-1.0f, 1.0f));
            if (!vec.IsZeroApprox())
            {
                return vec.Normalized();
            }
        }

        return Vector3.Up;
    }

    /// <summary>Returns a normalized random direction on the XZ plane (Y = 0). Useful for horizontal scatter.</summary>
    public static Vector3 GetRndVector3ZeroY()
    {
        var rnd2 = GetRndVector2();
        var vec = new Vector3(rnd2.X, 0f, rnd2.Y);
        return vec.IsZeroApprox() ? Vector3.Right : vec.Normalized();
    }
}
