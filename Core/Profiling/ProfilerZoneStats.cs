namespace Jmodot.Core.Profiling;

/// <summary>
/// Accumulated timing statistics for a single named profiler zone.
/// Pure logic — no engine dependency, fully unit-testable. Fed elapsed microseconds
/// per sample; tracks count, total, min/max/last, and a derived average.
/// </summary>
public sealed class ProfilerZoneStats
{
    public long Count { get; private set; }
    public long TotalUsec { get; private set; }
    public long MinUsec { get; private set; }
    public long MaxUsec { get; private set; }
    public long LastUsec { get; private set; }

    public double AverageUsec => this.Count == 0 ? 0.0 : (double)this.TotalUsec / this.Count;

    public void Record(long elapsedUsec)
    {
        // Seed min/max on the first sample so the comparisons below never read a zero floor.
        if (this.Count == 0)
        {
            this.MinUsec = elapsedUsec;
            this.MaxUsec = elapsedUsec;
        }

        if (elapsedUsec < this.MinUsec) { this.MinUsec = elapsedUsec; }
        if (elapsedUsec > this.MaxUsec) { this.MaxUsec = elapsedUsec; }

        this.LastUsec = elapsedUsec;
        this.TotalUsec += elapsedUsec;
        this.Count++;
    }
}
