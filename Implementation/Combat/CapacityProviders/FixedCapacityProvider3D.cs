namespace Jmodot.Implementation.Combat.CapacityProviders;

using Godot;
using Jmodot.Core.Stats;

/// <summary>
/// Fixed-value capacity cap, ignoring stats. Returns <c>true</c> while
/// <c>hitsAcceptedSoFar &lt; MaxHits</c>.
///
/// <para>
/// <b>Use cases.</b> Hard caps that don't compose with traits — typically AOE grenades
/// or scripted attacks where the cap is part of the spell's identity, not a tunable knob:
/// <list type="bullet">
///   <item>"Cluster bomb hits exactly 5 enemies" — <c>MaxHits = 5</c>.</item>
///   <item>"Static lance pierces exactly 2 targets" — <c>MaxHits = 2</c>.</item>
///   <item>"Lockpick hits one target" — <c>MaxHits = 1</c>.</item>
/// </list>
/// For trait-amplifiable caps, use <see cref="StatCapacityProvider3D"/> instead.
/// </para>
///
/// <para>
/// <b>Stats independence.</b> The <c>stats</c> argument is unused; this provider operates
/// purely on the count parameter. Safe for use on hitboxes attached to attackers without
/// an <see cref="IStatProvider"/> (e.g., environmental damage zones).
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class FixedCapacityProvider3D : HitboxCapacityProvider3D
{
    /// <summary>Maximum hits the hitbox will accept in a single attack session. Must be > 0;
    /// values ≤ 0 effectively disable the hitbox (no hits accepted).</summary>
    [Export] public int MaxHits { get; private set; } = 1;

    public override bool CanAcceptMoreHits(int hitsAcceptedSoFar, IStatProvider? stats, Node? owner)
    {
        return hitsAcceptedSoFar < MaxHits;
    }

    #region Test Helpers
#if TOOLS
    internal void SetMaxHits(int max) => MaxHits = max;
#endif
    #endregion
}
