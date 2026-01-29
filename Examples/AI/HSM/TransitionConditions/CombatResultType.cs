namespace Jmodot.Examples.AI.HSM.TransitionConditions;

/// <summary>
/// Specifies which CombatResult subtype to filter for in a CombatCondition.
/// </summary>
public enum CombatResultType
{
    /// <summary>Matches any CombatResult regardless of subtype.</summary>
    Any,
    /// <summary>Matches only DamageResult events.</summary>
    Damage,
    /// <summary>Matches only StatusResult events (status applied).</summary>
    Status,
    /// <summary>Matches only StatusExpiredResult events.</summary>
    StatusExpired,
    /// <summary>Matches only HealResult events.</summary>
    Heal,
    /// <summary>Matches only StatResult events.</summary>
    Stat,
    /// <summary>Matches only EffectResult events.</summary>
    Effect
}
