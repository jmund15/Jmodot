namespace Jmodot.Implementation.Profiling;

using System;
using Godot;
using Jmodot.Core.Profiling;

/// <summary>
/// A disposable timing scope created via <see cref="JmoProfiler.Sample"/>. Measures the
/// wall-clock duration between construction and disposal and reports it to the active backend.
/// Use with a <c>using</c> statement so timing closes deterministically:
/// <code>using (JmoProfiler.Sample("PoolBurst")) { /* work to measure */ }</code>
/// A scope with a null backend (profiling disabled) is a cheap no-op — it captures no timestamp
/// and records nothing.
/// </summary>
public readonly struct ProfilerScope : IDisposable
{
    private readonly IProfilerBackend? _backend;
    private readonly string _zone;
    private readonly ulong _startUsec;

    internal ProfilerScope(string zone, IProfilerBackend? backend)
    {
        this._zone = zone;
        this._backend = backend;

        if (backend == null)
        {
            this._startUsec = 0UL;
            return;
        }

        this._startUsec = Time.GetTicksUsec();
        backend.OnZoneBegin(zone);
    }

    public void Dispose()
    {
        if (this._backend == null) { return; }

        long elapsed = (long)(Time.GetTicksUsec() - this._startUsec);
        this._backend.OnZoneEnd(this._zone, elapsed);
    }
}
