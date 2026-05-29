namespace Jmodot.Implementation.Physics.Collision;

using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Attribute = Jmodot.Core.Stats.Attribute;

/// <summary>
/// One entry in a <see cref="StatChainResponse"/>'s prioritized dispatch chain.
/// When the spell's stat for <see cref="DispatchStat"/> is positive (&gt; 0), the chain selects
/// this entry's <see cref="Response"/>. Authors order entries by precedence (first-match wins).
/// </summary>
[Tool, GlobalClass]
public partial class StatGatedEntry : Resource
{
    /// <summary>Int attribute whose runtime value gates this entry (e.g. pierce_count, wall_bounce_count).</summary>
    [Export, RequiredExport] public Attribute DispatchStat { get; private set; } = null!;

    /// <summary>The collision response selected when DispatchStat is positive.</summary>
    [Export, RequiredExport] public BaseCollisionResponse Response { get; private set; } = null!;

    #region Test Helpers
#if TOOLS
    internal void SetDispatchStat(Attribute attr) => DispatchStat = attr;
    internal void SetResponse(BaseCollisionResponse response) => Response = response;
#endif
    #endregion
}
