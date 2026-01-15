namespace Jmodot.Core.Combat.Status;

/// <summary>
/// Interface for status runners whose effect can be amplified/intensified.
/// Used by Amplify interaction effects.
/// </summary>
public interface IAmplifiable
{
    /// <summary>
    /// Amplifies this status effect by the given magnitude.
    /// Implementation varies by runner type:
    /// - Duration runners: extend duration by magnitude seconds
    /// - Tick runners: could increase damage or extend duration
    /// </summary>
    /// <param name="magnitude">The amplification amount.</param>
    void Amplify(float magnitude);
}
