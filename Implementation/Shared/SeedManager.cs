namespace Jmodot.Implementation.Shared;

using System;
using System.Globalization;
using Godot;

/// <summary>
/// Game-wide master seed for deterministic, reproducible runs.
/// Derives per-system sub-seeds so each system's randomness is isolated.
/// The root seed comes from a <c>--seed=N</c> user argument, else MasterSeed, else a unique
/// auto-generated seed per session (<see cref="ResolveRootSeed"/>).
/// </summary>
public partial class SeedManager : Node
{
    [Export] public int MasterSeed { get; set; } = 0;

    public static SeedManager? Instance { get; private set; }
    private int _activeSeed;
    public int ActiveSeed => _activeSeed;

    /// <summary>
    /// True once <see cref="ActiveSeed"/> has been assigned (in <see cref="_Ready"/>).
    /// Distinguishes "seed is set" from "seed is the int default 0" — consumers that
    /// derive from <see cref="ActiveSeed"/> must gate on this to catch a bind-order bug
    /// (reading the seed before this autoload's _Ready ran) rather than silently deriving
    /// from 0.
    /// </summary>
    public bool HasActiveSeed { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        var (seed, source) = ResolveRootSeed(OS.GetCmdlineUserArgs(), MasterSeed, () => (int)GD.Randi());
        if (source == RootSeedSource.Random)
        {
            JmoLogger.Warning(this, $"[SeedManager] No {SeedUserArg}=N argument and MasterSeed=0 — auto-generated seed; replay it with {SeedUserArg}={seed} or set MasterSeed in the inspector.");
        }
        _activeSeed = seed;
        HasActiveSeed = true;
        JmoLogger.Info(this, $"[SeedManager] SeedManager initialized. Active seed: {_activeSeed} (source: {source.ToString().ToLowerInvariant()})");
    }

    public const string SeedUserArg = "--seed";

    public enum RootSeedSource { Cmdline, Inspector, Random }

    /// <summary>
    /// Picks the run's root seed: a nonzero integer <c>--seed=N</c> user argument, then a nonzero
    /// <paramref name="masterSeed"/>, then <paramref name="rollRandom"/>. A non-integer or zero
    /// <c>--seed</c> logs a Warning naming the argument and falls through. Pure apart from that
    /// Warning; <paramref name="rollRandom"/> is invoked only when both earlier sources are absent.
    /// </summary>
    public static (int Seed, RootSeedSource Source) ResolveRootSeed(string[] userArgs, int masterSeed, Func<int> rollRandom)
    {
        ArgumentNullException.ThrowIfNull(rollRandom);

        if (UserArgs.TryGetValue(userArgs, SeedUserArg, out var raw))
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                JmoLogger.Warning(nameof(SeedManager), $"[SeedManager] Ignoring {SeedUserArg}={raw}: not an integer. Falling back to MasterSeed or a random seed.");
            }
            else if (parsed == 0)
            {
                JmoLogger.Warning(nameof(SeedManager), $"[SeedManager] Ignoring {SeedUserArg}={raw}: 0 means unseeded. Falling back to MasterSeed or a random seed.");
            }
            else
            {
                return (parsed, RootSeedSource.Cmdline);
            }
        }

        if (masterSeed != 0) { return (masterSeed, RootSeedSource.Inspector); }

        return (rollRandom(), RootSeedSource.Random);
    }

    public override void _ExitTree()
    {
        if (Instance == this) { Instance = null; }
    }

    /// <summary>
    /// Derive a system-specific seed from a master seed.
    /// Pure function — testable in Logic Domain without Node lifecycle.
    /// Uses a stable FNV-1a-style mix; MUST NOT use <c>HashCode.Combine</c>,
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

    /// <summary>
    /// Hit-path lineage derivation: folds <paramref name="label"/> as a string segment,
    /// then folds <paramref name="index"/> directly as an int (no stringification).
    /// This is the int-segment index domain — deliberately distinct from
    /// <see cref="SeedSequence"/>'s stringified-counter path; the two do NOT compose.
    /// Use for hot-path keys (e.g. per-hit derivation) where the index is a raw int.
    /// </summary>
    public static int DeriveChild(int parentSeed, string label, int index)
    {
        unchecked
        {
            int hash = DeriveSystemSeed(parentSeed, label);
            hash = (hash * 16777619) ^ index;
            return hash;
        }
    }

    /// <summary>
    /// Two-int-segment hit-path derivation: folds <paramref name="label"/> as a string
    /// segment, then <paramref name="index1"/> and <paramref name="index2"/> directly as
    /// ints in order. Order-sensitive — (a,b) and (b,a) derive distinct seeds. Same
    /// int-segment index domain as <see cref="DeriveChild(int, string, int)"/>.
    /// </summary>
    public static int DeriveChild(int parentSeed, string label, int index1, int index2)
    {
        unchecked
        {
            int hash = DeriveSystemSeed(parentSeed, label);
            hash = (hash * 16777619) ^ index1;
            hash = (hash * 16777619) ^ index2;
            return hash;
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetActiveSeedForTesting(int value)
    {
        _activeSeed = value;
        HasActiveSeed = true;
    }

    internal static void SetInstanceForTesting(SeedManager? instance) => Instance = instance;
#endif
    #endregion
}
