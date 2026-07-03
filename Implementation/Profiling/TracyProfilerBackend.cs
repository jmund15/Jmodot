namespace Jmodot.Implementation.Profiling;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.Profiling;

/// <summary>
/// Streams the backend-agnostic <see cref="IProfilerBackend"/> events into a live Tracy GUI via a
/// C# Tracy client, on a stock Godot runtime. Bridges the flat seam (begin/end carry only a name)
/// to Tracy's per-thread LIFO zone model with a <see cref="ThreadStaticAttribute"/> handle stack,
/// and lets Tracy stamp begin/end timestamps itself — so the seam's measured <c>elapsedUsec</c> is
/// ignored entirely in favor of Tracy's own high-resolution timeline.
///
/// <para><b>Godot-free invariant:</b> the constructor and all four <c>On*</c> methods touch zero
/// Godot API (no <c>JmoLogger</c>, no <see cref="ProjectSettings"/>, no <see cref="OS"/>), so
/// the mapping logic is pure-CLR mock-testable without the engine runtime. All native interop and
/// degradation logging live in <see cref="TracyNative"/>; the ProjectSettings statics below are
/// invoked only from the game's autoload, never from a test.</para>
/// </summary>
public sealed class TracyProfilerBackend : IProfilerBackend
{
    private const string PROFILER_TRACY_ENABLED_SETTING = "debug/jmodot/profiler_tracy_enabled";

    // Tracy zones nest per-thread; each thread owns its own begin/end handle stack.
    [ThreadStatic]
    private static Stack<ulong>? _zoneStack;

    private readonly ITracyNative _native;
    private readonly bool _available;

    public TracyProfilerBackend(ITracyNative? native = null)
    {
        this._native = native ?? new TracyNative();
        this._available = this._native.IsAvailable;
    }

    private static Stack<ulong> ZoneStack => _zoneStack ??= new Stack<ulong>();

    public void OnZoneBegin(string zone)
    {
        if (!this._available) { return; }

        // The flat seam supplies only the zone name; no caller source coords are available here.
        ulong handle = this._native.ZoneBegin(zone, 0, zone, string.Empty);
        ZoneStack.Push(handle);
    }

    public void OnZoneEnd(string zone, long elapsedUsec)
    {
        if (!this._available) { return; }

        // Defensive: an end with no matching begin (e.g. profiling toggled on mid-scope) must NOT
        // call native — a garbage ctx would corrupt Tracy's per-thread stack. Silent by design.
        if (ZoneStack.Count == 0) { return; }

        ulong handle = ZoneStack.Pop();
        this._native.ZoneEnd(handle);
    }

    public void OnPlot(string name, double value)
    {
        if (!this._available) { return; }

        this._native.Plot(name, value);
    }

    public void OnFrameMark()
    {
        if (!this._available) { return; }

        this._native.FrameMark();
    }

    #region Project Settings (invoked only from the game autoload — Godot-touching)

    private static bool? _enabledCache;

    /// <summary>
    /// Cached read of the editor toggle <c>Project Settings → Debug → Jmodot → Profiler Tracy
    /// Enabled</c>. Defaults to false, so omitting the setting keeps the default aggregating backend.
    /// </summary>
    public static bool EnabledInProjectSettings =>
        _enabledCache ??= (bool)ProjectSettings.GetSetting(PROFILER_TRACY_ENABLED_SETTING, false);

    /// <summary>
    /// Registers the Tracy-enabled setting with ProjectSettings so it appears in the editor UI as a
    /// checkbox. Mirrors <c>JmoLogger.RegisterProjectSettings</c>; call once from the autoload.
    /// </summary>
    public static void RegisterProjectSettings()
    {
        if (!ProjectSettings.HasSetting(PROFILER_TRACY_ENABLED_SETTING))
        {
            ProjectSettings.SetSetting(PROFILER_TRACY_ENABLED_SETTING, false);
        }

        var propertyInfo = new Godot.Collections.Dictionary
        {
            { "name", PROFILER_TRACY_ENABLED_SETTING },
            { "type", (int)Variant.Type.Bool }
        };
        ProjectSettings.AddPropertyInfo(propertyInfo);
        ProjectSettings.SetAsBasic(PROFILER_TRACY_ENABLED_SETTING, true);

        _enabledCache = (bool)ProjectSettings.GetSetting(PROFILER_TRACY_ENABLED_SETTING, false);
    }

    #endregion

    #region Test Helpers
#if TOOLS
    /// <summary>Clears the calling thread's zone stack so mapping tests start from a known state.</summary>
    internal static void ClearThreadZoneStackForTesting() => _zoneStack?.Clear();

    /// <summary>Clears the cached ProjectSettings value so a future test re-reads it fresh.</summary>
    internal static void ResetEnabledCacheForTesting() => _enabledCache = null;
#endif
    #endregion
}
