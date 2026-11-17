namespace Jmodot.Core.Combat;

using Implementation.Shared;
using Reactions;

/// <summary>
/// The scene-side receiver of every <see cref="ImpactReport"/> a combatant or the spell contact path
/// publishes, whether or not a victim combatant exists to carry it on its own event.
/// </summary>
public interface IHitReportSink
{
    void Receive(ImpactReport report);
}

/// <summary>
/// Outbound seam for impact reports: one static sink the project installs (typically from an
/// autoload's <c>_EnterTree</c>). Null drops reports silently, which Jmodot-only and test scenes rely on.
/// </summary>
public static class HitReportSeam
{
    public static IHitReportSink? Sink;

    private static bool _missingProfileWarned;

    /// <summary>Clears the sink and the missing-profile latch. Intended for test teardown.</summary>
    public static void Reset()
    {
        Sink = null;
        _missingProfileWarned = false;
    }

    /// <summary>
    /// Logs the one Warning that no <see cref="ImpactWeightProfile"/> is wired. Returns true on the call
    /// that logged it, false on every later call until <see cref="Reset"/>.
    /// </summary>
    internal static bool TryWarnMissingProfile(Godot.Node owner)
    {
        if (_missingProfileWarned) { return false; }
        _missingProfileWarned = true;
        JmoLogger.Warning(owner, "no ImpactWeightProfile wired; no impact reports will publish");
        return true;
    }
}
