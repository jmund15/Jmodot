namespace Jmodot.Core.Math;

using System;

/// <summary>
/// Pure static budget calculation utilities for systems that allocate discrete items
/// from a continuous budget (fragment emission, ingredient drops, loot tables).
///
/// Scale + Clamp model: Clamp(allocation × intensity × (1 ± variation), min, max).
/// All methods accept explicit random values for deterministic testing.
/// </summary>
public static class BudgetCalculator
{
    /// <summary>
    /// Calculates a scaled budget using the Scale + Clamp model.
    /// Formula: Clamp(allocation × max(intensity, 0) × (1 ± variation), min, max).
    /// When variation is 0, the result is fully deterministic regardless of randomValue.
    /// </summary>
    /// <param name="intensity">How intense the event is (0-1 typical, unclamped).</param>
    /// <param name="allocation">Base budget at full intensity (1.0).</param>
    /// <param name="variation">Randomization range (±%). 0 = deterministic.</param>
    /// <param name="min">Floor clamp.</param>
    /// <param name="max">Ceiling clamp.</param>
    /// <param name="randomValue">Random value in [0,1] for variation. 0.5 = no change.</param>
    public static float CalculateScaledBudget(
        float intensity, float allocation, float variation, float min, float max, float randomValue)
    {
        float rawBudget = allocation * System.Math.Max(intensity, 0f);

        if (variation > 0f)
        {
            float randomOffset = (randomValue * 2f - 1f) * variation;
            rawBudget *= (1f + randomOffset);
        }

        return System.Math.Clamp(rawBudget, min, max);
    }

    /// <summary>
    /// Calculates a budget uniformly distributed between min and max.
    /// Used when budget is independent of event intensity.
    /// </summary>
    /// <param name="min">Minimum budget.</param>
    /// <param name="max">Maximum budget.</param>
    /// <param name="randomValue">Random value in [0,1]. 0 = min, 1 = max.</param>
    public static float CalculateRandomBudget(float min, float max, float randomValue)
    {
        return min + randomValue * (max - min);
    }

    /// <summary>
    /// Checks whether enough time has elapsed since the last trigger for cooldown.
    /// Returns true if (currentTime - lastTriggerTime) >= cooldownSeconds.
    /// </summary>
    /// <param name="lastTriggerTime">Time of last trigger (-1 sentinel = never triggered).</param>
    /// <param name="currentTime">Current time.</param>
    /// <param name="cooldownSeconds">Required cooldown duration.</param>
    public static bool IsCooldownReady(double lastTriggerTime, double currentTime, double cooldownSeconds)
    {
        return (currentTime - lastTriggerTime) >= cooldownSeconds;
    }
}
