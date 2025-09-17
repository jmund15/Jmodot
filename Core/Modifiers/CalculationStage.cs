namespace Jmodot.Core.Modifiers;

/// <summary>
///     Defines the strict, non-overridable order of operations for calculating a final stat value.
///     This ensures that all modifications are applied in a predictable, stable, and mathematically
///     sound manner across the entire framework, preventing order-of-operations bugs.
///     The numeric values ensure a correct sort order.
/// </summary>
public enum CalculationStage
{
    /// <summary>
    ///     Stage 1: Flat bonuses are applied directly to the base value.
    ///     Use this for effects like "+10 Damage" from a piece of equipment.
    /// </summary>
    BaseAdd = 100,

    /// <summary>
    ///     Stage 2: All percentage-based bonuses are summed together and then applied as a single multiplier.
    ///     Use this for effects like "+10% Max Health" from a perk. (A +10% and +20% buff results in a +30% total bonus).
    /// </summary>
    PercentAdd = 200,

    /// <summary>
    ///     Stage 3: Final, independent multipliers are applied in order of priority.
    ///     Use this for critical, situational effects like "*2 for a Critical Hit" or "*0 for a Stun".
    /// </summary>
    FinalMultiply = 300
}
