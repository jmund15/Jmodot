namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using GCol = Godot.Collections;

/// <summary>
/// Stat-driven dispatcher between sub-responses. Walks <see cref="Chain"/> in order; the first
/// entry whose <c>DispatchStat</c> resolves to a positive value selects that entry's response.
/// If no entry's stat is positive, <see cref="Fallback"/> is selected.
///
/// This lets the archetype declare the SUPERSET of possible collision behaviors at design time
/// (e.g. an entity mapping that could pierce, then bounce, then destroy). Traits select which
/// subset is active by amping the corresponding chain stats. The chain order is the designer's
/// precedence convention; tier-1 stats default chain stats to 0 so the chain falls through to
/// Fallback (giving the baseline single-target behavior).
///
/// Usage: a runner unwraps StatChainResponse via <see cref="ResolveDispatch"/> at collision time,
/// then dispatches the resulting concrete response normally.
/// </summary>
[Tool, GlobalClass]
public partial class StatChainResponse : BaseCollisionResponse
{
    /// <summary>Prioritized chain of stat-gated entries.</summary>
    [Export] public GCol.Array<StatGatedEntry> Chain { get; private set; } = new();

    /// <summary>
    /// Selected when no chain entry's DispatchStat is positive. Typically a leaf
    /// <c>DestroyCollisionResponse</c> for "single-target on contact" behavior.
    /// </summary>
    [Export, RequiredExport] public BaseCollisionResponse Fallback { get; private set; } = null!;

    /// <summary>
    /// Resolves the chain against the supplied stat provider, returning the concrete response
    /// the consumer should dispatch. Returns Fallback if no entry's stat is positive (or if
    /// stats are unavailable). Returns null only when the configuration is broken (no Fallback).
    /// </summary>
    public BaseCollisionResponse? ResolveDispatch(IStatProvider? stats)
    {
        if (stats == null) { return Fallback; }

        foreach (var entry in Chain)
        {
            if (entry == null || entry.DispatchStat == null || entry.Response == null) { continue; }
            if (stats.GetStatValue<int>(entry.DispatchStat, defaultValue: 0) > 0)
            {
                return entry.Response;
            }
        }
        return Fallback;
    }

    #region Test Helpers
#if TOOLS
    internal void SetChain(GCol.Array<StatGatedEntry> chain) => Chain = chain;
    internal void SetFallback(BaseCollisionResponse fallback) => Fallback = fallback;
#endif
    #endregion
}
