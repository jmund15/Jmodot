namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Core.Combat;
using Core.Combat.EffectDefinitions;
using Core.Combat.Reactions;
using Core.Stats;
using Core.Shared.Attributes;
using Implementation.AI.BB;
using Implementation.Combat;
using Implementation.Shared;

/// <summary>
/// Routes direct hits after the emitting state's entry by their peak weight: the sum at each hit's arrival, with
/// earlier hits decayed by their gap to it. A hit keeps its band until it leaves the window, however late the check runs.
/// </summary>
[GlobalClass, Tool]
public partial class ImpactWeightCondition : CombatLogCondition, Jmodot.Core.Shared.IResourceConfigurationWarnings
{
    /// <summary>
    /// Defers this transition while the victim's push is still held, or released but not yet applied by its
    /// movement, so a launch-strength hit cannot match a lower tier first. It only defers: the weighing window stays Within Seconds. Off lets the
    /// transition fire during the hold, for an edge that must react at the hit itself.
    /// </summary>
    [Export] public bool DeferWhileHeld { get; set; } = true;

    /// <summary>Inclusive lower weight bound.</summary>
    [Export, RequiredExport] public BaseFloatValueDefinition MinWeight { get; private set; } = null!;

    /// <summary>Exclusive upper bound; absent means no upper bound.</summary>
    [Export] public BaseFloatValueDefinition? MaxWeight { get; private set; }

    /// <summary>Stat added to both bounds; an absent stat value reads as zero.</summary>
    [Export, RequiredExport] public Jmodot.Core.Stats.Attribute PoiseAttribute { get; private set; } = null!;

    /// <summary>
    /// Combat-time window: hits closer together than this add up, each decayed linearly by its gap, and a hit
    /// stops counting once it is this old. Keep it longer than the victim's longest push hold, or a held hit expires unrouted.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,2,0.05,suffix:s")] public float WithinSeconds { get; private set; } = 0.5f;

    /// <summary>
    /// A hit counts once it is this old in combat time, which advances once per physics tick. Transitions are
    /// checked every process frame but a hit's knockback lands in the next physics tick, so an edge ordered after
    /// the launch edge sets a value above zero to let the launch edge see the velocity first. Zero routes at once.
    /// </summary>
    [Export(PropertyHint.Range, "0,0.1,0.005,suffix:s")] public float SettleSeconds { get; private set; }

    /// <summary>Only count results logged after the emitting state entered; disable on escalation edges.</summary>
    [Export] public bool CountOnlyAfterStateEntry { get; private set; } = true;

    public override bool Check(Node agent, IBlackboard bb) => Check(agent, bb, CombatLogMark.Origin);

    public override bool Check(Node agent, IBlackboard bb, in CombatLogMark entry)
    {
        this.ValidateRequiredExports();
        if (DeferWhileHeld
            && KnockbackComponent3D.TryGetHeldPushWindow(bb, out _, out var holding)
            && holding)
        {
            return false;
        }
        if (!bb.TryGet<CombatLog>(BBDataSig.CombatLog, out var log) || log == null) { return false; }
        bb.TryGet<IStatProvider>(BBDataSig.Stats, out var stats);
        float poise = stats?.GetStatValue(PoiseAttribute, 0f) ?? 0f;
        float min = MinWeight.ResolveFloatValue(stats) + poise;
        float? max = MaxWeight?.ResolveFloatValue(stats) + poise;
        if (!float.IsFinite(min) || !float.IsFinite(poise)
            || (max.HasValue && (!float.IsFinite(max.Value) || max.Value <= min))) { return false; }
        var mark = CountOnlyAfterStateEntry ? entry : CombatLogMark.Origin;
        float qualifying = log.PeakDecayedSince<HitReport>(mark, WithinSeconds, static r => r.Sustained ? 0f : 1f, SettleSeconds);
        if (!(qualifying > 0f)) { return false; }
        float weight = log.PeakDecayedSince<HitReport>(mark, WithinSeconds, static r => r.Sustained ? 0f : r.Weight, SettleSeconds);
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
    internal void ConfigureForTesting(BaseFloatValueDefinition min, BaseFloatValueDefinition? max, Jmodot.Core.Stats.Attribute poise, bool entryOnly = true, float settleSeconds = 0f)
    {
        SettleSeconds = settleSeconds;
        MinWeight = min;
        MaxWeight = max;
        PoiseAttribute = poise;
        CountOnlyAfterStateEntry = entryOnly;
    }
#endif
    #endregion
}
