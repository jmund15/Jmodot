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
public partial class ImpactWeightCondition : CombatLogCondition, Jmodot.Core.Shared.IResourceConfigurationWarnings
{
    /// <summary>Inclusive lower weight bound.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition MinWeight { get; private set; } = null!;

    /// <summary>Exclusive upper bound; absent means no upper bound.</summary>
    [Export] public BaseFloatValueDefinition? MaxWeight { get; private set; }

    /// <summary>Stat added to both bounds; an absent stat value reads as zero.</summary>
    [Export, RequiredExport] public Jmodot.Core.Stats.Attribute PoiseAttribute { get; private set; } = null!;

    /// <summary>Combat-time window for linear decay.</summary>
    [Export(PropertyHint.Range, "0.05,2,0.05,suffix:s")] public float WithinSeconds { get; private set; } = 0.5f;

    /// <summary>Only count results logged after the emitting state entered; disable on escalation edges.</summary>
    [Export] public bool CountOnlyAfterStateEntry { get; private set; } = true;

    public override bool Check(Node agent, IBlackboard bb) => Check(agent, bb, CombatLogMark.Origin);

    public override bool Check(Node agent, IBlackboard bb, in CombatLogMark entry)
    {
        this.ValidateRequiredExports();
        if (!bb.TryGet<CombatLog>(BBDataSig.CombatLog, out var log) || log == null) { return false; }
        bb.TryGet<IStatProvider>(BBDataSig.Stats, out var stats);
        float poise = stats?.GetStatValue(PoiseAttribute, 0f) ?? 0f;
        float min = MinWeight.ResolveFloatValue(stats) + poise;
        float? max = MaxWeight?.ResolveFloatValue(stats) + poise;
        if (!float.IsFinite(min) || !float.IsFinite(poise)
            || (max.HasValue && (!float.IsFinite(max.Value) || max.Value <= min))) { return false; }
        var mark = CountOnlyAfterStateEntry ? entry : CombatLogMark.Origin;
        float qualifying = log.SumDecayedSince<HitReport>(mark, WithinSeconds, static r => r.Sustained ? 0f : 1f);
        if (!(qualifying > 0f)) { return false; }
        float weight = log.SumDecayedSince<HitReport>(mark, WithinSeconds, static r => r.Sustained ? 0f : r.Weight);
        return float.IsFinite(weight) && weight >= min && (!max.HasValue || weight < max.Value);
    }

    public string[] GetResourceConfigurationWarnings()
    {
        var warnings = new System.Collections.Generic.List<string>();
        if (MinWeight == null) { warnings.Add("Min Weight is required."); }
        if (PoiseAttribute == null) { warnings.Add("Poise Attribute is required."); }
        if (!(WithinSeconds > 0f) || !float.IsFinite(WithinSeconds)) { warnings.Add("Within Seconds must be finite and positive."); }
        return warnings.ToArray();
    }

    protected override bool CheckEvent(CombatLog log) => false;

    #region Test Helpers
#if TOOLS
    internal void ConfigureForTesting(BaseFloatValueDefinition min, BaseFloatValueDefinition? max, Jmodot.Core.Stats.Attribute poise, bool entryOnly = true)
    {
        MinWeight = min;
        MaxWeight = max;
        PoiseAttribute = poise;
        CountOnlyAfterStateEntry = entryOnly;
    }
#endif
    #endregion
}
