namespace Jmodot.Core.Combat.Status;

/// <summary>
/// Interface for status runners whose duration can be modified at runtime.
/// Used by interaction effects like ReduceDuration and Amplify.
/// </summary>
public interface IDurationModifiable
{
    /// <summary>
    /// The remaining duration of this status effect in seconds.
    /// </summary>
    float RemainingDuration { get; }

    /// <summary>
    /// Reduces the duration by the specified amount.
    /// Duration cannot go below 0.
    /// </summary>
    /// <param name="amount">Amount in seconds to reduce.</param>
    void ReduceDuration(float amount);

    /// <summary>
    /// Extends the duration by the specified amount.
    /// </summary>
    /// <param name="amount">Amount in seconds to extend.</param>
    void ExtendDuration(float amount);

    /// <summary>
    /// Sets the duration to a specific value.
    /// </summary>
    /// <param name="newDuration">The new duration in seconds.</param>
    void SetDuration(float newDuration);
}
