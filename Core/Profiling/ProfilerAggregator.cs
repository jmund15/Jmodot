namespace Jmodot.Core.Profiling;

using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Accumulates <see cref="ProfilerZoneStats"/> keyed by zone name. Pure logic — no engine
/// dependency. Owns report formatting so the rendered table is unit-testable independent of any
/// logging sink.
/// </summary>
public sealed class ProfilerAggregator
{
    private readonly Dictionary<string, ProfilerZoneStats> _zones = new();

    public int ZoneCount => this._zones.Count;

    public void Record(string zone, long elapsedUsec)
    {
        if (!this._zones.TryGetValue(zone, out var stats))
        {
            stats = new ProfilerZoneStats();
            this._zones[zone] = stats;
        }

        stats.Record(elapsedUsec);
    }

    public bool TryGetStats(string zone, out ProfilerZoneStats? stats) => this._zones.TryGetValue(zone, out stats);

    public void Reset() => this._zones.Clear();

    /// <summary>
    /// Renders a fixed-width table of all zones, sorted by total time descending. Times are shown
    /// in milliseconds (total) and microseconds (avg/min/max/last) for readability. The leading
    /// <c>[Profiler]</c> tag keeps the output sliceable by /analyze_godot_logs.
    /// </summary>
    public string FormatReport()
    {
        if (this._zones.Count == 0)
        {
            return "[Profiler] no zones recorded";
        }

        var sb = new StringBuilder();
        sb.AppendLine("[Profiler] zone report (sorted by total time):");
        sb.AppendLine($"  {"zone",-32} {"count",8} {"total ms",12} {"avg us",12} {"min us",10} {"max us",10} {"last us",10}");
        foreach (var entry in this._zones.OrderByDescending(z => z.Value.TotalUsec))
        {
            var s = entry.Value;
            sb.AppendLine(
                $"  {entry.Key,-32} {s.Count,8} {s.TotalUsec / 1000.0,12:F3} {s.AverageUsec,12:F1} {s.MinUsec,10} {s.MaxUsec,10} {s.LastUsec,10}");
        }

        return sb.ToString();
    }
}
