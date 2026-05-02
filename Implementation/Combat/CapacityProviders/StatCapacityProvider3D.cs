namespace Jmodot.Implementation.Combat.CapacityProviders;

using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Attribute = Jmodot.Core.Stats.Attribute;

/// <summary>
/// Capacity cap driven by a runtime <see cref="Attribute"/> read from the spell's
/// <see cref="IStatProvider"/>. Effective cap = <c>stats.GetStatValue&lt;int&gt;(CapacityStat) + Offset</c>.
///
/// <para>
/// <b>Use cases.</b> Trait-amplifiable caps that must compose with stat modifiers:
/// <list type="bullet">
///   <item>Single-target spell with pierce trait — <c>CapacityStat = pierce_count</c>, <c>Offset = +1</c>:
///     baseline (stat 0) → cap 1 (single-hit); pierce trait amps stat to 3 → cap 4.</item>
///   <item>AOE grenade with designer-tunable max-targets — <c>CapacityStat = aoe_max_targets</c>,
///     <c>Offset = 0</c>: cap = stat value; "Splash Bigger" trait amps stat → larger AOE.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Dynamic semantic.</b> Stats are read on every <see cref="CanAcceptMoreHits"/> call,
/// so a decrementing <c>pierce_count</c> mid-attack tightens the cap immediately — once
/// the stat reaches 0 and one hit has been accepted, no further hits are allowed even
/// within the same physics tick. This matches the user-required "MUST be single hit on
/// the last pierce" semantic.
/// </para>
///
/// <para>
/// <b>Fail-closed when stats unavailable.</b> If <paramref name="stats"/> is null OR
/// <see cref="CapacityStat"/> is null, the provider returns <c>false</c> — degenerate
/// config must not silently flip the hitbox into "unlimited mode". Diagnostic logging
/// is the hitbox's responsibility (once-per-StartAttack); providers stay stateless and
/// log-free so the per-call cost is just a comparison.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class StatCapacityProvider3D : HitboxCapacityProvider3D
{
    /// <summary>The integer attribute whose runtime value drives the cap (e.g., <c>pierce_count</c>).</summary>
    [Export, RequiredExport] public Attribute CapacityStat { get; private set; } = null!;

    /// <summary>Constant added to the stat value when computing the cap. Default <c>1</c> so
    /// that <c>stat=0</c> produces cap=1 (single-hit baseline) for pierce-style attributes.
    /// Set to <c>0</c> for stats that already represent the absolute hit cap.</summary>
    [Export] public int Offset { get; private set; } = 1;

    public override bool CanAcceptMoreHits(int hitsAcceptedSoFar, IStatProvider? stats, Node? owner)
    {
        if (CapacityStat == null) { return false; }
        if (stats == null) { return false; }

        int cap = stats.GetStatValue<int>(CapacityStat, 0) + Offset;
        return hitsAcceptedSoFar < cap;
    }

    #region Test Helpers
#if TOOLS
    internal void SetCapacityStat(Attribute attr) => CapacityStat = attr;
    internal void SetOffset(int offset) => Offset = offset;
#endif
    #endregion
}
