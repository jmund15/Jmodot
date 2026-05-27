namespace Jmodot.Core.Modifiers.StageRules;

using System.Collections.Generic;

/// <summary>
///     Summed-percentage stage (int): each value is a whole-number percent, summed then applied once with
///     <c>Mathf.RoundToInt(running * ((100 + Σv) / 100f))</c> — the exact pre-refactor int rounding.
/// </summary>
[GlobalClass, Tool]
public partial class IntSummedPercentStageRule : IntModifierStageRule
{
    /// <summary>Cap on the accumulated bonus, as a whole-number percent. <c>&lt;= 0</c> means uncapped.</summary>
    [Export] public int MaxBonusPercent { get; private set; }

    public IntSummedPercentStageRule()
    {
        StageId = "PercentAdd";
        Order = 200;
        NeutralValue = 0;
    }

    public IntSummedPercentStageRule(int maxBonusPercent) : this()
    {
        MaxBonusPercent = maxBonusPercent;
    }

    public override int Reduce(int running, IReadOnlyList<int> stageValues)
    {
        var bonus = 0;
        foreach (var value in stageValues)
        {
            bonus += value;
        }
        if (MaxBonusPercent > 0)
        {
            bonus = Mathf.Min(bonus, MaxBonusPercent);
        }
        return Mathf.RoundToInt(running * ((100 + bonus) / 100f));
    }
}
