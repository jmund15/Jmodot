namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Core.Combat;
using Core.Combat.EffectDefinitions;
using Core.Combat.Reactions;
using Core.Stats;
using Core.Shared.Attributes;
using Implementation.AI.BB;
using Implementation.Shared;

/// <summary>Routes direct hits by their decayed weight after the emitting state's entry.</summary>
[GlobalClass, Tool]
public partial class ImpactWeightCondition : CombatLogCondition
{
    /// <summary>Inclusive lower weight bound.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition MinWeight { get; private set; } = null!;

    /// <summary>Combat-time window for linear decay.</summary>
    [Export(PropertyHint.Range, "0.05,2,0.05,suffix:s")] public float WithinSeconds { get; private set; } = 0.5f;

    /// <summary>Only count results logged after the emitting state entered; disable on escalation edges.</summary>
    [Export] public bool CountOnlyAfterStateEntry { get; private set; } = true;

    public override bool Check(Node agent, IBlackboard bb) => Check(agent, bb, CombatLogMark.Origin);

    public override bool Check(Node agent, IBlackboard bb, in CombatLogMark entry)
    {
        if (!bb.TryGet<CombatLog>(BBDataSig.CombatLog, out var log) || log == null) { return false; }
        bb.TryGet<IStatProvider>(BBDataSig.Stats, out var stats);
        var mark = CountOnlyAfterStateEntry ? entry : CombatLogMark.Origin;
        float weight = log.SumDecayedSince<HitReport>(mark, WithinSeconds, static r => r.Sustained ? 0f : r.Weight);
        return weight >= MinWeight.ResolveFloatValue(stats);
    }

    protected override bool CheckEvent(CombatLog log) => false;

    #region Test Helpers
#if TOOLS
    internal void SetMinimumForTesting(BaseFloatValueDefinition value) => MinWeight = value;
#endif
    #endregion
}
