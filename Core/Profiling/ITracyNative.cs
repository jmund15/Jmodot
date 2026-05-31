namespace Jmodot.Core.Profiling;

/// <summary>
/// Port over the handful of native Tracy calls the profiler backend needs. Wrapping them behind
/// an interface keeps the backend's mapping logic (LIFO zone stack, availability gating, string
/// interning) pure-CLR mock-testable, while the real P/Invoke (<c>TracyNative</c>) stays a thin,
/// untested adapter. The <see cref="ZoneBegin"/> handle is an opaque <see cref="ulong"/> so this
/// Logic-Domain contract never names a <c>Tracy.*</c> type — the adapter maps it back to the
/// real zone-context struct internally.
/// </summary>
public interface ITracyNative
{
    /// <summary>True when the native library loaded and is callable. False => the backend hard no-ops.</summary>
    bool IsAvailable { get; }

    /// <summary>Begin a zone; returns an opaque handle the backend pairs with the matching <see cref="ZoneEnd"/>.</summary>
    ulong ZoneBegin(string zone, int line, string member, string file);

    /// <summary>End the zone identified by a handle previously returned from <see cref="ZoneBegin"/>.</summary>
    void ZoneEnd(ulong ctxHandle);

    /// <summary>Record a named scalar plot at the current instant.</summary>
    void Plot(string name, double value);

    /// <summary>Mark a frame boundary on Tracy's timeline.</summary>
    void FrameMark();
}
