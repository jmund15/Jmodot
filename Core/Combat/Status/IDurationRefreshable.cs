namespace Jmodot.Core.Combat.Status;

using Jmodot.Implementation.Combat.Status;

/// <summary>
/// Interface for status runners whose duration can be refreshed from another source.
/// Used when RefreshOldest overflow behavior is triggered.
/// </summary>
public interface IDurationRefreshable
{
    /// <summary>
    /// Refreshes this status's duration using the source runner's duration.
    /// Does NOT re-trigger OnStart/OnEnd effects.
    /// </summary>
    /// <param name="source">The incoming runner to refresh from.</param>
    void RefreshDuration(StatusRunner source);
}
