namespace Jmodot.Core.Combat;

using System;
using Godot;

/// <summary>
/// Configures how effects with the same CombatTag stack.
/// Attached to CombatTag as optional configuration.
/// </summary>
[GlobalClass]
public partial class StackPolicy : Resource
{
    /// <summary>
    /// Maximum simultaneous instances. 0 = unlimited, 1 = non-stacking.
    /// </summary>
    [Export] public int MaxStacks { get; private set; } = 0;

    /// <summary>
    /// What happens when MaxStacks is reached.
    /// </summary>
    [Export] public StackOverflowBehavior OverflowBehavior { get; private set; } = StackOverflowBehavior.Reject;

    /// <summary>
    /// How duration is handled for stacking effects.
    /// </summary>
    [Export] public DurationStackBehavior DurationBehavior { get; private set; } = DurationStackBehavior.Independent;

    /// <summary>
    /// Maximum total duration when using Extend behavior. 0 = unlimited.
    /// </summary>
    [Export] public float MaxTotalDuration { get; private set; } = 0f;

    /// <summary>
    /// Evaluates whether a new effect can be added given current stack count.
    /// Returns the action to take.
    /// </summary>
    /// <param name="currentStackCount">Number of existing instances with this tag.</param>
    /// <returns>Result indicating what action to take.</returns>
    public StackPolicyResult Evaluate(int currentStackCount)
    {
        // Unlimited stacking - always accept
        if (MaxStacks <= 0)
        {
            return StackPolicyResult.Accept();
        }

        // Under limit - accept
        if (currentStackCount < MaxStacks)
        {
            return StackPolicyResult.Accept();
        }

        // At or over limit - apply overflow behavior
        return OverflowBehavior switch
        {
            StackOverflowBehavior.Reject => StackPolicyResult.Reject(),
            StackOverflowBehavior.RefreshOldest => StackPolicyResult.RefreshOldest(),
            StackOverflowBehavior.ReplaceOldest => StackPolicyResult.ReplaceOldest(),
            _ => StackPolicyResult.Reject()
        };
    }

    /// <summary>
    /// Calculates the new duration based on DurationBehavior.
    /// </summary>
    /// <param name="existingDuration">Remaining duration of the existing effect.</param>
    /// <param name="incomingDuration">Duration of the incoming effect.</param>
    /// <returns>The calculated duration value.</returns>
    public float CalculateDuration(float existingDuration, float incomingDuration)
    {
        return DurationBehavior switch
        {
            DurationStackBehavior.Independent => incomingDuration,
            DurationStackBehavior.Extend => ClampDuration(existingDuration + incomingDuration),
            DurationStackBehavior.Refresh => incomingDuration,
            DurationStackBehavior.Max => Math.Max(existingDuration, incomingDuration),
            DurationStackBehavior.Reject => existingDuration,
            _ => incomingDuration
        };
    }

    private float ClampDuration(float duration)
    {
        if (MaxTotalDuration <= 0)
        {
            return duration;
        }

        return Math.Min(duration, MaxTotalDuration);
    }

    #region Test Helpers

    /// <summary>Sets MaxStacks for testing purposes.</summary>
    internal void SetMaxStacks(int value) => MaxStacks = value;

    /// <summary>Sets OverflowBehavior for testing purposes.</summary>
    internal void SetOverflowBehavior(StackOverflowBehavior value) => OverflowBehavior = value;

    /// <summary>Sets DurationBehavior for testing purposes.</summary>
    internal void SetDurationBehavior(DurationStackBehavior value) => DurationBehavior = value;

    /// <summary>Sets MaxTotalDuration for testing purposes.</summary>
    internal void SetMaxTotalDuration(float value) => MaxTotalDuration = value;

    #endregion
}
