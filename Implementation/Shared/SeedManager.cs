namespace Jmodot.Implementation.Shared;

using System;
using Godot;

/// <summary>
/// Game-wide master seed for deterministic, reproducible runs.
/// Derives per-system sub-seeds so each system's randomness is isolated.
/// MasterSeed=0 auto-generates a unique seed per session.
/// </summary>
public partial class SeedManager : Node
{
    [Export] public int MasterSeed { get; set; } = 0;

    public static SeedManager? Instance { get; private set; }
    private int _activeSeed;
    public int ActiveSeed => _activeSeed;

    public override void _Ready()
    {
        Instance = this;
        if (MasterSeed == 0)
        {
            JmoLogger.Warning(this, "MasterSeed=0 — auto-generated seed; variation will not be reproducible across runs. Set MasterSeed in the inspector for deterministic runs.");
            _activeSeed = (int)GD.Randi();
        }
        else
        {
            _activeSeed = MasterSeed;
        }
        JmoLogger.Info(this, $"SeedManager initialized. Active seed: {_activeSeed}");
    }

    public override void _ExitTree()
    {
        if (Instance == this) { Instance = null; }
    }

    /// <summary>
    /// Derive a system-specific seed from a master seed.
    /// Pure function — testable in Logic Domain without Node lifecycle.
    /// Uses a stable FNV-1a-style mix; MUST NOT use <see cref="HashCode.Combine"/>,
    /// which is process-randomized and breaks cross-session reproducibility.
    /// </summary>
    public static int DeriveSystemSeed(int masterSeed, string systemName)
    {
        unchecked
        {
            int hash = (int)2166136261;
            hash = (hash * 16777619) ^ masterSeed;
            foreach (char c in systemName) { hash = (hash * 16777619) ^ c; }
            return hash;
        }
    }

    /// <summary>
    /// Hierarchical lineage derivation: folds <paramref name="path"/> segments
    /// sequentially through <see cref="DeriveSystemSeed"/>. Order-sensitive.
    /// <para>
    /// Empty path returns <paramref name="parent"/> unchanged (identity).
    /// Null or empty segments throw <see cref="ArgumentException"/> to prevent
    /// silent seed-collision between paths that differ only by empty segments.
    /// </para>
    /// </summary>
    public static int DeriveChild(int parent, params string[] path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        int hash = parent;
        foreach (var segment in path)
        {
            if (string.IsNullOrEmpty(segment))
            {
                throw new ArgumentException(
                    "DeriveChild path segments must be non-null and non-empty.",
                    nameof(path));
            }
            hash = DeriveSystemSeed(hash, segment);
        }
        return hash;
    }

    #region Test Helpers
#if TOOLS
    internal void SetActiveSeedForTesting(int value) => _activeSeed = value;
    internal static void SetInstanceForTesting(SeedManager? instance) => Instance = instance;
#endif
    #endregion
}
