namespace Jmodot.Core.Profiling;

/// <summary>
/// Default <see cref="IProfilerBackend"/>: accumulates per-zone timing in-process via a
/// <see cref="ProfilerAggregator"/>. Pure logic, always available — no engine build flags,
/// works on a stock Godot build. Zone-begin / plot / frame-mark are no-ops here; they exist for
/// streaming backends (Tracy) that present a live timeline.
/// </summary>
public sealed class AggregatingProfilerBackend : IProfilerBackend
{
    public ProfilerAggregator Aggregator { get; } = new();

    public void OnZoneBegin(string zone) { }

    public void OnZoneEnd(string zone, long elapsedUsec) => this.Aggregator.Record(zone, elapsedUsec);

    public void OnPlot(string name, double value) { }

    public void OnFrameMark() { }
}
