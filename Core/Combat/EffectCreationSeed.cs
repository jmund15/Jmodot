namespace Jmodot.Core.Combat;

using System;

/// <summary>
/// The lineage seed handed to <see cref="ICombatEffectFactory.Create"/> at effect-assembly time.
/// A null <c>EffectCreationSeed?</c> argument maps to <see cref="SeedProvenance.Missing"/> — the
/// optional-parameter shape lets the existing factory implementers stay unforced.
/// <para>
/// <see cref="Resolution"/> selects WHEN crit is rolled, and the single backing value's meaning
/// follows it: on <see cref="CritResolution.Resolved"/> it is the pre-derived crit-roll seed
/// (<c>DeriveChild(attackSeed,"crit",effectIdx)</c>), read via <see cref="CritRollSeed"/>; on
/// <see cref="CritResolution.DeferredPerHit"/> it is the <c>effectIdx</c> folded with the per-hit
/// <c>HitSeed</c> at apply time, read via <see cref="EffectIndex"/>. Always construct through the
/// intent-named factories (<see cref="ForResolved"/> / <see cref="ForDeferred"/>) so the value's
/// meaning is unambiguous at the call site; the guarded accessors throw if read against the wrong
/// resolution rather than silently returning the other meaning (a swapped seed is the cardinal
/// determinism bug). A zero-init <c>default</c> means "resolve at creation" — see
/// <see cref="CritResolution.Resolved"/> being enum value 0.
/// </para>
/// </summary>
public readonly struct EffectCreationSeed
{
    /// <summary>Whether crit is rolled at assembly (Resolved) or deferred to per-hit <c>Apply</c> (DeferredPerHit).</summary>
    public CritResolution Resolution { get; }

    // Meaning follows Resolution (crit-roll seed when Resolved, effect index when DeferredPerHit);
    // exposed only through the guarded CritRollSeed / EffectIndex accessors so no read site can
    // silently take the wrong meaning.
    private readonly int _value;

    /// <summary>
    /// Kept <c>internal</c> so production constructs through <see cref="ForResolved"/> /
    /// <see cref="ForDeferred"/>; the factories below are the only callers.
    /// </summary>
    internal EffectCreationSeed(int value, CritResolution resolution)
    {
        _value = value;
        Resolution = resolution;
    }

    /// <summary>A Resolved-path seed carrying the pre-derived crit-roll seed.</summary>
    public static EffectCreationSeed ForResolved(int critRollSeed) => new(critRollSeed, CritResolution.Resolved);

    /// <summary>A DeferredPerHit-path seed carrying the effect index folded with the per-hit HitSeed.</summary>
    public static EffectCreationSeed ForDeferred(int effectIndex) => new(effectIndex, CritResolution.DeferredPerHit);

    /// <summary>The pre-derived crit-roll seed. Valid only on the <see cref="CritResolution.Resolved"/> path.</summary>
    /// <exception cref="InvalidOperationException">Read against a <see cref="CritResolution.DeferredPerHit"/> seed.</exception>
    public int CritRollSeed => Resolution == CritResolution.Resolved
        ? _value
        : throw new InvalidOperationException(
            $"CritRollSeed is valid only for {nameof(CritResolution.Resolved)} seeds (was {Resolution}).");

    /// <summary>The effect index folded with the per-hit HitSeed. Valid only on the <see cref="CritResolution.DeferredPerHit"/> path.</summary>
    /// <exception cref="InvalidOperationException">Read against a <see cref="CritResolution.Resolved"/> seed.</exception>
    public int EffectIndex => Resolution == CritResolution.DeferredPerHit
        ? _value
        : throw new InvalidOperationException(
            $"EffectIndex is valid only for {nameof(CritResolution.DeferredPerHit)} seeds (was {Resolution}).");
}
