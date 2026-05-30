namespace Jmodot.Core.Profiling;

/// <summary>
/// Pluggable sink for profiler events. The default <see cref="AggregatingProfilerBackend"/>
/// accumulates per-zone statistics in-process and works on a stock Godot build. A future
/// Tracy-client backend can implement this same interface to emit live zones to the Tracy GUI
/// without changing any instrumentation call site — Godot's native Tracy integration profiles
/// only C++ engine internals and requires a source build, so a C#-side backend is the route to
/// Tracy's GUI for gameplay code.
/// </summary>
public interface IProfilerBackend
{
    /// <summary>A profiled scope opened. Aggregating backends ignore this; streaming (Tracy) backends begin a live zone.</summary>
    void OnZoneBegin(string zone);

    /// <summary>A profiled scope closed, carrying its measured duration in microseconds.</summary>
    void OnZoneEnd(string zone, long elapsedUsec);

    /// <summary>Record a named scalar at the current instant (e.g. live projectile count, queue depth).</summary>
    void OnPlot(string name, double value);

    /// <summary>Mark a frame boundary for backends that present a per-frame timeline.</summary>
    void OnFrameMark();
}
