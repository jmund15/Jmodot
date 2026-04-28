namespace Jmodot.Examples.AI.BehaviorTree.Conditions;

using System.Linq;
using Core.AI.BehaviorTree.Conditions;
using Core.Combat;
using Core.Combat.Reactions;
using Godot;
using HSM.TransitionConditions;
using Implementation.AI.BB;
using Implementation.Shared;

/// <summary>
/// BTCondition that returns true when a CombatResult of the configured type and
/// magnitude occurred within the configured time window. Reads from CombatLog on
/// the BB; pairs with CombatLogger on the entity to populate the log.
/// <para>
/// Typical use is a gate on AttackSequence.Conditions with Inverted=true and
/// UrgentAbort=true so any hit aborts the in-flight attack. Threshold exports
/// (MinForce, MinDamage) make stability-style gating a pure data tweak.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class RecentHitCondition : BTCondition
{
    [ExportGroup("Filters")]
    [Export] public CombatResultType ResultType { get; set; } = CombatResultType.Damage;

    [Export(PropertyHint.Range, "0.01,10,0.01")]
    public float WindowSeconds { get; set; } = 0.1f;

    /// <summary>
    /// Minimum DamageResult.Force required to register as a hit. 0 = ignore force.
    /// Force is a DamageResult-only field; with ResultType=Any and MinForce>0,
    /// non-DamageResult events (Heal/Status/etc.) are excluded — set ResultType=Damage
    /// explicitly when gating on force to avoid the surprise.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float MinForce { get; set; }

    /// <summary>
    /// Minimum DamageResult.FinalAmount required to register as a hit. 0 = ignore damage.
    /// Damage is a DamageResult-only field; same Any-vs-Damage interaction as MinForce.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float MinDamage { get; set; }

    /// <summary>
    /// Flips the entire result. Typical use on AttackSequence.Conditions sets this true
    /// so the gate passes when NO recent hit (task continues) and fails on hit (aborts).
    /// </summary>
    [ExportGroup("Gate")]
    [Export] public bool Inverted { get; set; }

    public override bool Check()
    {
        if (!BB.TryGet(BBDataSig.CombatLog, out CombatLog? log) || log == null)
        {
            // No log on BB → no hits could have been recorded.
            // Honour Inverted so a missing CombatLogger doesn't permanently lock down
            // any task that's gated as "abort if recent hit" via Inverted=true.
            return Inverted;
        }

        var hit = log
            .GetAllCombatResultsWithinCombatTime<CombatResult>(WindowSeconds)
            .Any(r => MatchesType(r) && MatchesForce(r) && MatchesDamage(r));

        return Inverted ? !hit : hit;
    }

    private bool MatchesType(CombatResult r) => ResultType switch
    {
        CombatResultType.Any => true,
        CombatResultType.Damage => r is DamageResult,
        CombatResultType.Heal => r is HealResult,
        CombatResultType.Status => r is StatusResult,
        CombatResultType.StatusExpired => r is StatusExpiredResult,
        CombatResultType.Stat => r is StatResult,
        CombatResultType.Effect => r is EffectResult,
        _ => true,
    };

    private bool MatchesForce(CombatResult r) =>
        MinForce <= 0f || (r is DamageResult d && d.Force >= MinForce);

    private bool MatchesDamage(CombatResult r) =>
        MinDamage <= 0f || (r is DamageResult d && d.FinalAmount >= MinDamage);
}
