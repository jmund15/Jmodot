namespace Jmodot.Implementation.Physics;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Per-physics-frame deduplication of resolved elastic collision pairs.
/// When both entities in a collision have ImpactCollisionStrategy, each will
/// attempt to resolve the same pair. The first to call TryClaimPair wins;
/// the second gets false and skips resolution.
/// Auto-resets when the physics frame advances. Use Reset() for testing.
/// </summary>
public static class ImpactFrameTracker
{
    private static readonly HashSet<(ulong, ulong)> _claimedPairs = new();
    private static ulong _lastFrame = ulong.MaxValue;

    /// <summary>Override for testing. Set to non-null to bypass Engine.GetPhysicsFrames().</summary>
    internal static ulong? TestFrameOverride;

    /// <summary>
    /// Attempts to claim an entity pair for resolution this frame.
    /// Returns true if this is the first claim (proceed with resolution).
    /// Returns false if the pair was already resolved (skip).
    /// IDs are canonicalized: (min, max) to ensure A-B == B-A.
    /// </summary>
    public static bool TryClaimPair(ulong idA, ulong idB)
    {
        ulong currentFrame = TestFrameOverride ?? Engine.GetPhysicsFrames();
        if (currentFrame != _lastFrame)
        {
            _claimedPairs.Clear();
            _lastFrame = currentFrame;
        }

        var pair = idA < idB ? (idA, idB) : (idB, idA);
        return _claimedPairs.Add(pair);
    }

    /// <summary>Clears all tracked pairs and resets frame state. For testing only.</summary>
    public static void Reset()
    {
        _claimedPairs.Clear();
        _lastFrame = ulong.MaxValue;
        TestFrameOverride = null;
    }
}
