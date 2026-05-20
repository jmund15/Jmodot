namespace Jmodot.Implementation.Shared;

using System;
using Godot;

/// <summary>
/// Seeded instance RNG wrapping <see cref="RandomNumberGenerator"/>. Every game
/// randomness consumer holds its own instance; same seed → same sequence, always.
/// <para>
/// Determinism contract: <see cref="FromRawStreamName"/> derives a stream-isolated seed via
/// <see cref="SeedManager.DeriveChild"/>; <see cref="NonDeterministic"/> is the
/// Pos 3 migration target (every call site invoking it is a known non-deterministic
/// site to be replaced — <c>Grep("NonDeterministic\(")</c> for the current backlog).
/// </para>
/// <para>
/// Lifecycle: <see cref="RandomNumberGenerator"/> is a Godot <c>RefCounted</c>; the
/// private field is the strong reference, so GC + Godot refcount handle disposal.
/// JmoRng does NOT implement <see cref="IDisposable"/> — no unmanaged resources
/// beyond the RefCounted, and the underlying generator is never exposed.
/// </para>
/// </summary>
public sealed class JmoRng
{
    private readonly RandomNumberGenerator _rng;
    private const int MaxAttempts = 10;

    /// <summary>
    /// Construct a seeded RNG. Negative <paramref name="seed"/> values wrap to high
    /// <c>ulong</c> via unchecked cast (consistent and reproducible — relied on by
    /// <see cref="NonDeterministic"/> which seeds with <c>Guid.GetHashCode()</c>).
    /// </summary>
    public JmoRng(int seed)
    {
        _rng = new RandomNumberGenerator { Seed = (ulong)seed };
    }

    /// <summary>Returns a random float in [0, 1).</summary>
    public float GetRndFloat() => _rng.Randf();

    /// <summary>Returns a random int in [0, max). For array indexing.</summary>
    public int GetRndInt(int max) => _rng.RandiRange(0, max - 1);

    /// <summary>Returns a random int in [min, max] (inclusive both ends).</summary>
    public int GetRndInRange(int min, int max) => _rng.RandiRange(min, max);

    /// <summary>
    /// Returns a random float in [min, max). Max is exclusive (System.Random.NextSingle
    /// scaling), unlike <see cref="RandomNumberGenerator.RandfRange"/> which is inclusive.
    /// </summary>
    public float GetRndInRange(float min, float max)
    {
        if (min > max)
        {
            throw new ArgumentException($"min ({min}) must not be greater than max ({max}).");
        }
        return _rng.Randf() * (max - min) + min;
    }

    /// <summary>Returns +1f or -1f randomly.</summary>
    public float GetRndSign() => _rng.RandiRange(0, 1) == 0 ? 1f : -1f;

    /// <summary>Returns a normalized random 2D direction vector with components in [-1, 1).</summary>
    public Vector2 GetRndVector2()
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            var vec = new Vector2(GetRndInRange(-1.0f, 1.0f), GetRndInRange(-1.0f, 1.0f));
            if (!vec.IsZeroApprox()) { return vec.Normalized(); }
        }
        return Vector2.Right;
    }

    /// <summary>Returns a normalized random 3D direction vector with components in [-1, 1).</summary>
    public Vector3 GetRndVector3()
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            var vec = new Vector3(GetRndInRange(-1.0f, 1.0f), GetRndInRange(-1.0f, 1.0f), GetRndInRange(-1.0f, 1.0f));
            if (!vec.IsZeroApprox()) { return vec.Normalized(); }
        }
        return Vector3.Right;
    }

    /// <summary>Returns a normalized random 3D direction with positive Y (upward hemisphere). Y in [0, 1).</summary>
    public Vector3 GetRndVector3PosY()
    {
        for (int i = 0; i < MaxAttempts; i++)
        {
            var vec = new Vector3(GetRndInRange(-1.0f, 1.0f), GetRndInRange(0f, 1.0f), GetRndInRange(-1.0f, 1.0f));
            if (!vec.IsZeroApprox()) { return vec.Normalized(); }
        }
        return Vector3.Up;
    }

    /// <summary>Returns a normalized random direction on the XZ plane (Y = 0). Useful for horizontal scatter.</summary>
    public Vector3 GetRndVector3ZeroY()
    {
        var rnd2 = GetRndVector2();
        var vec = new Vector3(rnd2.X, 0f, rnd2.Y);
        return vec.IsZeroApprox() ? Vector3.Right : vec.Normalized();
    }

    /// <summary>
    /// Deterministic factory: derives a per-stream seed from <paramref name="parentSeed"/>
    /// via <see cref="SeedManager.DeriveChild"/> and constructs a seeded instance.
    /// Same (streamName, parentSeed) pair yields the same sequence, always.
    /// <para>
    /// Consuming-project convention: prefer the project's strongly-typed stream
    /// registry over raw string calls. A typo'd <paramref name="streamName"/>
    /// silently yields a different seed — the registry indirection is what makes
    /// "adding a top-level stream requires code review" enforceable (PushinPotions
    /// uses <c>SeedStreams.X.CreateRng(parentSeed)</c>; raw <c>FromRawStreamName("X", ...)</c>
    /// escapes that gate).
    /// </para>
    /// </summary>
    public static JmoRng FromRawStreamName(string streamName, int parentSeed)
        => new JmoRng(SeedManager.DeriveChild(parentSeed, streamName));

    /// <summary>
    /// Pos 3 migration target — every call-site invoking this factory is a known
    /// non-deterministic site to be replaced with a properly seeded instance via
    /// constructor injection, Blackboard slot, or <see cref="FromRawStreamName"/>.
    /// Grep <c>NonDeterministic\(</c> for the current backlog.
    /// </summary>
    public static JmoRng NonDeterministic()
        => new JmoRng(Guid.NewGuid().GetHashCode());
}
