namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>
///     Summed-percentage stage: each value is a whole-number percent (10 = +10%). All values are summed,
///     optionally capped by <see cref="MaxBonusPercent" />, then applied once as a single multiplier.
///     Summing then applying once (not compounding per-modifier) is the load-bearing parity contract.
/// </summary>
[GlobalClass, Tool]
public partial class SummedPercentStageRule : FloatModifierStageRule
{
    /// <summary>Cap on the accumulated bonus, as a whole-number percent. <c>&lt;= 0</c> means uncapped.</summary>
    // Sentinel (<=0 = uncapped) not a sentinel-default like +inf: a value-type [Export] left unauthored
    // can resave/load as 0, so 0 must be the safe "no cap" state.
    [Export] public float MaxBonusPercent { get; private set; }

    public SummedPercentStageRule()
    {
        StageId = "PercentAdd";
        Order = 200;
        NeutralValue = 0f;
    }

    public SummedPercentStageRule(float maxBonusPercent) : this()
    {
        MaxBonusPercent = maxBonusPercent;
    }

    public override float Reduce(float running, IReadOnlyList<float> stageValues)
    {
        var bonus = 0f;
        foreach (var value in stageValues)
        {
            bonus += value / 100f;
        }
        if (MaxBonusPercent > 0f)
        {
            bonus = Mathf.Min(bonus, MaxBonusPercent / 100f);
        }
        return running * (1f + bonus);
    }
}
