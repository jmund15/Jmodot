namespace Jmodot.Implementation.AI.Navigation;

/// <summary>
/// Collapses a repeating unreachable-target condition into one warning per episode.
/// </summary>
/// <remarks>
/// Nav requests are re-issued every physics frame while a target stays unreachable, so warning at the
/// call site emits one line per frame for as long as the condition lasts. The retries themselves are
/// wanted — a target legitimately becomes reachable again — so the episode, not the request, is the
/// unit worth reporting.
/// </remarks>
public sealed class UnreachableWarningLatch
{
    private bool _episodeActive;
    private int _suppressedThisEpisode;
    private int _suppressedPreviousEpisode;

    /// <summary>
    /// True exactly once per unreachable episode. <paramref name="suppressedSinceLastWarning"/> carries
    /// how many requests the PREVIOUS episode muted, so the count is never lost to the silence.
    /// </summary>
    public bool ShouldWarn(out int suppressedSinceLastWarning)
    {
        if (_episodeActive)
        {
            _suppressedThisEpisode++;
            suppressedSinceLastWarning = 0;
            return false;
        }

        _episodeActive = true;
        suppressedSinceLastWarning = _suppressedPreviousEpisode;
        _suppressedPreviousEpisode = 0;
        return true;
    }

    /// <summary>Ends the current episode, re-arming the warning for the next one.</summary>
    public void Reset()
    {
        if (!_episodeActive)
        {
            return;
        }

        _episodeActive = false;
        _suppressedPreviousEpisode = _suppressedThisEpisode;
        _suppressedThisEpisode = 0;
    }
}
