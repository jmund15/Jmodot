namespace Jmodot.Implementation.Shared;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Stamps readable, per-label sequential names onto nodes as they enter the tree, so a runtime scene
/// tree and every log line built from a node path read as "Roach#3" rather than an engine-generated
/// "@CharacterBody3D@178".
///
/// <para>
/// Framework-agnostic by construction: the seam takes a plain string label, never a game type, so any
/// project can name its own spawns through it. Counters are process-global and monotonic per label —
/// they are display identity, not identity the game may key on.
/// </para>
///
/// <para><b>Not thread-safe.</b> Spawning is a main-thread operation; concurrent callers can observe a
/// duplicated sequence number.</para>
/// </summary>
public static class SpawnNaming
{
    private static readonly Dictionary<string, int> Counters = new();

    /// <summary>
    /// Names <paramref name="child"/> and then parents it. The order is load-bearing: Godot generates an
    /// <c>@</c>-prefixed name the moment an unnamed node is added, and a later rename leaves that name in
    /// any reference already resolved against it.
    /// </summary>
    public static void Attach(Node parent, Node child, string label)
    {
        if (parent == null || child == null) { return; }

        Stamp(child, label);
        parent.AddChild(child);
    }

    /// <summary>
    /// Stamp-only, for a node that is already parented — the pool-reactivation path, where a reused
    /// instance must take the next number rather than keep the one from its previous life.
    /// </summary>
    public static void Stamp(Node child, string label)
    {
        if (child == null) { return; }
        if (string.IsNullOrEmpty(label)) { return; }

        child.Name = NextName(label);
    }

    /// <summary>Clears every label counter. Test-isolation seam; production code should not call it.</summary>
    public static void Reset()
    {
        Counters.Clear();
    }

    private static string NextName(string label)
    {
        Counters.TryGetValue(label, out var count);
        count++;
        Counters[label] = count;
        return $"{label}#{count}";
    }
}
