namespace Jmodot.Implementation.Profiling;

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using bottlenoselabs.C2CS.Runtime;
using Jmodot.Core.Profiling;
using Jmodot.Implementation.Shared;
using Tracy;

/// <summary>
/// The sole interop site between <see cref="TracyProfilerBackend"/> and the native Tracy client
/// (<c>Tracy.PInvoke</c> / <c>CString</c>). Untested by design — exercised by the manual Tracy-GUI
/// protocol, not unit tests — so all native-string lifetime and availability concerns live here,
/// behind the mock-testable <see cref="ITracyNative"/> port.
///
/// <para><b>String lifetime</b> (per Tracy source): srcloc strings are <em>copied</em> by Tracy, so
/// zone begin uses transient <see cref="CString"/>s and disposes them immediately. Plot / frame
/// names are stored <em>by pointer, not copied</em>, so they are interned into never-freed
/// <see cref="CString"/>s via <see cref="NativeStringInterner{T}"/>.</para>
/// </summary>
public sealed class TracyNative : ITracyNative
{
    // The backend's per-thread stack guarantees begin/end pair on one thread, but distinct zones on
    // distinct threads still share this map + counter — hence the concurrent-safe primitives.
    private readonly ConcurrentDictionary<ulong, PInvoke.TracyCZoneCtx> _activeZones = new();
    private readonly NativeStringInterner<CString> _nameInterner = new(CString.FromString);
    private readonly object _internGate = new();
    private long _nextHandle;
    private volatile bool _degraded;

    public bool IsAvailable { get; }

    // Version-skew guard: the ctor probe proves only ONE symbol resolves (TracyConnected); the
    // runtime methods bind different natives, so a skewed client throws EntryPointNotFoundException
    // mid-gameplay. First throw latches _degraded (all four methods no-op from then on) — degrade,
    // never crash. Used as an exception FILTER so ZoneBegin's CString-disposal finally still runs.
    private bool LatchDegraded(Exception ex, string method)
    {
        if (!this._degraded)
        {
            this._degraded = true;
            JmoLogger.Warning(typeof(TracyNative),
                $"Tracy native call {method} failed ({ex.GetType().Name}); profiler degraded to no-op.");
        }
        return true;
    }

    public TracyNative()
    {
        try
        {
            // First P/Invoke forces the native lib to resolve+load; the connected-state result is
            // irrelevant (zones with no server attached are buffered/dropped — still "available").
            PInvoke.TracyConnected();
            this.IsAvailable = true;
        }
        catch (DllNotFoundException)
        {
            this.IsAvailable = false;
            JmoLogger.Warning(typeof(TracyNative), "Tracy native client not found; profiler backend will no-op.");
        }
        catch (Exception ex)
        {
            // EntryPointNotFound / BadImageFormat (wrong-arch RID) etc. — degrade, never crash.
            this.IsAvailable = false;
            JmoLogger.Warning(typeof(TracyNative), $"Tracy native client unavailable ({ex.GetType().Name}); profiler backend will no-op.");
        }
    }

    public ulong ZoneBegin(string zone, int line, string member, string file)
    {
        if (this._degraded) { return 0; }

        // Transient: Tracy copies srcloc strings into its own storage, so these are freed post-alloc.
        CString source = CString.FromString(file);
        CString function = CString.FromString(member);
        CString name = CString.FromString(zone);
        try
        {
            ulong srcloc = PInvoke.TracyAllocSrclocName(
                (uint)line,
                source, (ulong)Encoding.UTF8.GetByteCount(file),
                function, (ulong)Encoding.UTF8.GetByteCount(member),
                name, (ulong)Encoding.UTF8.GetByteCount(zone));

            PInvoke.TracyCZoneCtx ctx = PInvoke.TracyEmitZoneBeginAlloc(srcloc, 1);

            ulong handle = (ulong)Interlocked.Increment(ref this._nextHandle);
            this._activeZones[handle] = ctx;
            return handle;
        }
        catch (Exception ex) when (this.LatchDegraded(ex, nameof(ZoneBegin)))
        {
            return 0;
        }
        finally
        {
            source.Dispose();
            function.Dispose();
            name.Dispose();
        }
    }

    public void ZoneEnd(ulong ctxHandle)
    {
        if (this._degraded) { return; }

        try
        {
            if (this._activeZones.TryRemove(ctxHandle, out var ctx))
            {
                PInvoke.TracyEmitZoneEnd(ctx);
            }
        }
        catch (Exception ex) when (this.LatchDegraded(ex, nameof(ZoneEnd)))
        {
        }
    }

    public void Plot(string name, double value)
    {
        if (this._degraded) { return; }

        // Stored by pointer → must persist; the interner allocs once and never frees. OnPlot carries
        // no per-thread serialization (unlike zones), so guard the plain-Dictionary interner.
        try
        {
            CString interned;
            lock (this._internGate)
            {
                interned = this._nameInterner.Intern(name);
            }
            PInvoke.TracyEmitPlot(interned, value);
        }
        catch (Exception ex) when (this.LatchDegraded(ex, nameof(Plot)))
        {
        }
    }

    public void FrameMark()
    {
        if (this._degraded) { return; }

        try
        {
            // default(CString) is a null name → Tracy's default unnamed frame.
            PInvoke.TracyEmitFrameMark(default);
        }
        catch (Exception ex) when (this.LatchDegraded(ex, nameof(FrameMark)))
        {
        }
    }
}
