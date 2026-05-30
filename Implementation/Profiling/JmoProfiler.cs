namespace Jmodot.Implementation.Profiling;

using Jmodot.Core.Profiling;
using Jmodot.Implementation.Shared;

/// <summary>
/// Static facade for lightweight, always-available scope profiling on a stock Godot build.
/// Wrap any code region in <c>using (JmoProfiler.Sample("name")) { ... }</c>; durations
/// aggregate per zone and dump via <see cref="LogReport"/>.
///
/// <para><b>Disabled by default</b> (zero overhead in shipping). Set <see cref="Enabled"/> = true
/// at the start of a profiling session — from a debug menu, a test harness, or a launch flag.</para>
///
/// <para><b>Backend-swappable.</b> The default <see cref="AggregatingProfilerBackend"/> measures
/// your C# code with no engine recompile. Install a Tracy client backend via
/// <see cref="SetBackend"/> with zero changes to instrumentation sites: Godot's native Tracy
/// integration profiles only C++ engine internals and needs a source build, so a C#-side backend
/// is the path to the Tracy GUI for gameplay code.</para>
/// </summary>
public static class JmoProfiler
{
    private static IProfilerBackend _backend = new AggregatingProfilerBackend();

    /// <summary>When false (default), <see cref="Sample"/> returns a no-op scope and nothing is recorded.</summary>
    public static bool Enabled { get; set; }

    /// <summary>The active backend. Defaults to in-process aggregation; replace via <see cref="SetBackend"/>.</summary>
    public static IProfilerBackend Backend => _backend;

    /// <summary>Swap the sink (e.g. install a Tracy client backend). Null restores the default aggregating backend.</summary>
    public static void SetBackend(IProfilerBackend? backend) => _backend = backend ?? new AggregatingProfilerBackend();

    /// <summary>Open a timing scope. Disposing it (end of the <c>using</c> block) records the elapsed time.</summary>
    public static ProfilerScope Sample(string zone) => new ProfilerScope(zone, Enabled ? _backend : null);

    /// <summary>Record a named scalar at the current instant (live counts, queue depths). No-op when disabled.</summary>
    public static void Plot(string name, double value)
    {
        if (!Enabled) { return; }

        _backend.OnPlot(name, value);
    }

    /// <summary>Mark a frame boundary for timeline backends. No-op when disabled.</summary>
    public static void FrameMark()
    {
        if (!Enabled) { return; }

        _backend.OnFrameMark();
    }

    /// <summary>
    /// Log the accumulated zone report via <see cref="JmoLogger"/> (tag <c>[Profiler]</c>).
    /// Meaningful only for the aggregating backend; streaming backends present their own UI.
    /// </summary>
    public static void LogReport()
    {
        if (_backend is AggregatingProfilerBackend agg)
        {
            JmoLogger.Info(typeof(JmoProfiler), agg.Aggregator.FormatReport());
            return;
        }

        JmoLogger.Info(typeof(JmoProfiler), "[Profiler] active backend does not aggregate in-process — see its own UI");
    }

    /// <summary>Clear accumulated stats on the default aggregating backend. No-op for other backends.</summary>
    public static void ResetStats()
    {
        if (_backend is AggregatingProfilerBackend agg)
        {
            agg.Aggregator.Reset();
        }
    }

    #region Test Helpers
#if TOOLS
    internal static void ResetForTesting()
    {
        Enabled = false;
        _backend = new AggregatingProfilerBackend();
    }
#endif
    #endregion
}
