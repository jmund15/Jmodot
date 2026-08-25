namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using Core.AI.BB;
using Core.Shared.Attributes;
using Godot;
using Implementation.Shared;

/// <summary>
/// One named cooldown, authored once: the blackboard key the ready-at timestamp lives under and the
/// duration an arm buys. Writers call <see cref="Arm"/> when the gated event happens; readers (a
/// <see cref="BBCooldownReadyCondition"/>, or any other gate) call <see cref="IsReady"/>.
/// </summary>
/// <remarks>
/// The state is the ENTITY's — a timestamp on its own blackboard — so it survives behavior-tree
/// re-inits, condition re-clones, and state re-entries, none of which a per-instance latch survives.
/// Reference one shared <c>.tres</c> of this from every writer and reader of the same cooldown:
/// the key then has exactly one authored home and cannot be mistyped across surfaces.
/// </remarks>
[GlobalClass, Tool]
public partial class CooldownChannel : Resource
{
    /// <summary>Blackboard key the ready-at timestamp is stored under. Required — a channel with no key gates nothing.</summary>
    [Export, RequiredExport] public StringName ReadyAtKey { get; private set; } = null!;

    /// <summary>Seconds an <see cref="Arm"/> blocks for.</summary>
    [Export(PropertyHint.Range, "0.0, 60.0, 0.1, or_greater")]
    public float DurationSeconds { get; private set; } = 3f;

    private bool _validated;

    private static double NowSeconds() => Time.GetTicksMsec() / 1000.0;

    /// <summary>Starts (or restarts) the cooldown: ready again <see cref="DurationSeconds"/> from now.</summary>
    /// <exception cref="System.ArgumentNullException"><paramref name="bb"/> is null.</exception>
    public void Arm(IBlackboard bb)
    {
        System.ArgumentNullException.ThrowIfNull(bb);
        this.ValidateOnce();
        // Fail-closed on the authored duration: NaN passes every '< 0' test and a negative arm would
        // read as pre-elapsed — both collapse to a zero-length rest with one authored-data warning.
        var duration = this.DurationSeconds;
        if (!float.IsFinite(duration) || !(duration >= 0f))
        {
            JmoLogger.Warning(this, $"[Cooldown] DurationSeconds is {duration}; arming with 0 instead.");
            duration = 0f;
        }

        bb.Set(this.ReadyAtKey, NowSeconds() + duration);
    }

    /// <summary>Whether the cooldown has elapsed. A blackboard never armed is ready.</summary>
    /// <exception cref="System.ArgumentNullException"><paramref name="bb"/> is null.</exception>
    public bool IsReady(IBlackboard bb)
    {
        System.ArgumentNullException.ThrowIfNull(bb);
        this.ValidateOnce();
        if (!bb.TryGet<double>(this.ReadyAtKey, out var readyAt)) { return true; }

        return NowSeconds() >= readyAt;
    }

    // Reflection-backed export validation is too heavy for a per-BT-tick path — validate once per
    // instance; the latch is benign shared state (validation cannot un-pass).
    private void ValidateOnce()
    {
        if (this._validated) { return; }

        this.ValidateRequiredExports();
        this._validated = true;
    }

    #region Test Helpers
#if TOOLS
    internal void SetReadyAtKeyForTest(StringName value) => ReadyAtKey = value;
    internal void SetDurationSecondsForTest(float value) => DurationSeconds = value;
#endif
    #endregion
}
