namespace Jmodot.Implementation.Shared;
using Godot;
using System;
using System.Runtime.CompilerServices;
using Jmodot.Core.Identification;

/// <summary>
/// A centralized static class for logging with rich context. It standardizes message formats
/// across the project and leverages Godot's debugger for clear presentation of warnings and errors.
/// This is the definitive tool for all diagnostic output.
/// </summary>
public enum LogLevel
{
    Error,
    Warning,
    Info,
    Debug,
}

public static class JmoLogger
{
    private const string DEBUG_ENABLED_SETTING = "debug/jmodot/debug_logging_enabled";
    private const string MIN_LEVEL_SETTING = "debug/jmodot/minimum_log_level";

    #region Test Helpers
#if TOOLS
    /// <summary>
    /// Test-only fan-out invoked AFTER each production-sink write. Test code
    /// subscribes via JmoLoggerSpy; the (Level, Tag, Message) shape gives the
    /// spy enough to assert "no errors emitted from any subsystem" without
    /// pulling structured-log infrastructure into production builds.
    /// Tag = context type name (or string content when context IS a string).
    /// </summary>
    internal static event Action<LogLevel, string, string>? _TestOnLogEmitted;

    private static void EmitTestSignal(LogLevel level, object? context, string message)
    {
        var tag = context switch
        {
            null => "<null>",
            string s => s,
            _ => context.GetType().Name,
        };
        _TestOnLogEmitted?.Invoke(level, tag, message);
    }
#endif
    #endregion

    // Cache to avoid a ProjectSettings lookup on every call.
    private static LogLevel? _minimumLevelCache;

    /// <summary>
    /// The most verbose level that is emitted. <see cref="LogLevel"/> ascends in verbosity
    /// (Error → Debug), so a level is written when it is at or below this value.
    /// <see cref="LogLevel.Error"/> is never gated whatever this is set to.
    /// </summary>
    /// <remarks>
    /// Configure via Project Settings → Debug → Jmodot → Minimum Log Level. Defaults to
    /// <see cref="LogLevel.Info"/>, which is the pre-threshold behavior; drop it to
    /// <see cref="LogLevel.Warning"/> for a play session that should not pay for per-event output.
    /// </remarks>
    public static LogLevel MinimumLevel
    {
        get => _minimumLevelCache ??= ResolveMinimumLevel();
        set
        {
            _minimumLevelCache = value;
            ProjectSettings.SetSetting(MIN_LEVEL_SETTING, (int)value);
        }
    }

    /// <summary>
    /// Whether <see cref="Debug"/> messages are emitted — a view over <see cref="MinimumLevel"/>,
    /// not independent state. Setting it false returns the threshold to <see cref="LogLevel.Info"/>
    /// rather than silencing Info as well, matching what the flag meant before the threshold existed.
    /// </summary>
    public static bool DebugEnabled
    {
        get => MinimumLevel >= LogLevel.Debug;
        set => MinimumLevel = value ? LogLevel.Debug : LogLevel.Info;
    }

    /// <summary>Whether a call at <paramref name="level"/> will be written.</summary>
    public static bool IsEnabled(LogLevel level) => level <= MinimumLevel;

    // Migration fallback only. The bool is no longer registered as a visible setting (two editor
    // controls for one axis means whichever loses precedence looks broken), but a project that still
    // authors it keeps working until it adopts the threshold.
    private static LogLevel ResolveMinimumLevel()
    {
        if (ProjectSettings.HasSetting(MIN_LEVEL_SETTING))
        {
            return (LogLevel)(int)ProjectSettings.GetSetting(MIN_LEVEL_SETTING, (int)LogLevel.Info);
        }

        return (bool)ProjectSettings.GetSetting(DEBUG_ENABLED_SETTING, false)
            ? LogLevel.Debug
            : LogLevel.Info;
    }

    /// <summary>
    /// Registers <see cref="MinimumLevel"/> with ProjectSettings so it appears in the editor UI.
    /// Call this once during project initialization (e.g., from Global autoload).
    /// </summary>
    /// <remarks>
    /// The legacy <c>debug_logging_enabled</c> bool is deliberately NOT registered: two editor
    /// controls over one axis means whichever loses precedence looks broken to whoever ticked it.
    /// It is still read as a fallback for a project that has not adopted the threshold.
    /// </remarks>
    public static void RegisterProjectSettings()
    {
        if (!ProjectSettings.HasSetting(MIN_LEVEL_SETTING))
        {
            ProjectSettings.SetSetting(MIN_LEVEL_SETTING, (int)LogLevel.Info);
        }

        // Enum hint order must match LogLevel's declaration order — the setting stores the int.
        var levelInfo = new Godot.Collections.Dictionary
        {
            { "name", MIN_LEVEL_SETTING },
            { "type", (int)Variant.Type.Int },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", "Error,Warning,Info,Debug" }
        };
        ProjectSettings.AddPropertyInfo(levelInfo);
        ProjectSettings.SetAsBasic(MIN_LEVEL_SETTING, true);

        // Refresh cache from saved settings
        _minimumLevelCache = ResolveMinimumLevel();
    }
    /// <summary>
    /// The core private helper that builds the standardized log message string based on the context object's type.
    /// It enriches log messages with context about the source object and caller location information.
    /// </summary>
    /// <param name="level">The severity level of the log (e.g., "ERROR", "INFO").</param>
    /// <param name="context">The object that is the source of the log message.</param>
    /// <param name="message">The log message to be output.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object, for additional clarity.</param>
    /// <param name="callerFilePath">Auto-populated via CallerFilePath attribute. The source file where the log was called.</param>
    /// <param name="callerLineNumber">Auto-populated via CallerLineNumber attribute. The line number where the log was called.</param>
    /// <param name="callerMemberName">Auto-populated via CallerMemberName attribute. The method/property where the log was called.</param>
    /// <returns>A fully formatted string ready for output to the Godot console.</returns>
    private static string BuildLogMessage(
        string level,
        object? context,
        string message,
        Node? owner,
        string callerFilePath,
        int callerLineNumber,
        string callerMemberName)
    {
        // Gracefully handle cases where a null context is passed.
        if (context == null)
        {
            return $"[NULL CONTEXT] {level}: {message}";
        }

        string contextStr;
        string ownerStr;

        // Intelligently format the context string based on the object's type.
        switch (context)
        {
            case Node node:
                var nodeOwner = (node.IsInsideTree()) ? (owner ?? node.GetOwner()) : null;
                ownerStr = nodeOwner != null ? $" (Owner: {nodeOwner.GetPath()})" : "";
                if (node is ILogIdentity nodeIdentity && !string.IsNullOrEmpty(nodeIdentity.LogLabel))
                {
                    contextStr = $"[{nodeIdentity.LogLabel}]{ownerStr}";
                    break;
                }

                var pathStr = (node.IsInsideTree()) ? node.GetPath().ToString() : "[Detached]";
                contextStr = $"[{node.GetType().Name} @ '{pathStr}']{ownerStr}";
                break;
            case Resource resource:
                ownerStr = owner != null ? $" (Owner: {owner.GetPath()})" : "";
                contextStr = $"[{resource.GetType().Name} @ '{resource.ResourcePath}']{ownerStr}";
                break;
            default:
                if (context is ILogIdentity objectIdentity && !string.IsNullOrEmpty(objectIdentity.LogLabel))
                {
                    contextStr = $"[{objectIdentity.LogLabel}]";
                    break;
                }

                // If it overrides ToString(), use it. Otherwise use Type Name.
                var str = context.ToString() ?? string.Empty;
                // Check if default ToString (Namespace.ClassName) was returned
                if (str == context.GetType().ToString())
                {
                    contextStr = $"[{context.GetType().Name}]";
                }
                else { contextStr = $"[{context.GetType().Name}: {str}]"; }
                break;
        }

        // Extract just the filename from the full path for cleaner output.
        var fileName = System.IO.Path.GetFileName(callerFilePath);
        var callerInfo = $"{fileName}:{callerLineNumber} in {callerMemberName}()";

        return $"{contextStr} " +
               $"\n{level}: {message} @ {callerInfo}";
    }

    #region Error Logging (For Critical, Non-Functional Bugs)

    /// <summary>
    /// Logs a critical error. Use for setup, configuration, or runtime errors that
    /// prevent an object from functioning as intended.
    /// The error will be displayed prominently in Godot's debugger with full context.
    /// Prefer using string interpolation for dynamic values: JmoLogger.Error(this, $"Failed to load: {path}")
    /// </summary>
    /// <param name="context">The object (Node, Resource, etc.) that is the source of the error.</param>
    /// <param name="message">The error message. Use string interpolation for dynamic values.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object, for crucial context.</param>
    /// <param name="callerFilePath">Auto-populated. Do not pass manually.</param>
    /// <param name="callerLineNumber">Auto-populated. Do not pass manually.</param>
    /// <param name="callerMemberName">Auto-populated. Do not pass manually.</param>
    public static void Error(
        object context,
        string message,
        Node? owner = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        GD.PushError(BuildLogMessage("ERROR", context, message, owner, callerFilePath, callerLineNumber, callerMemberName));
#if TOOLS
        EmitTestSignal(LogLevel.Error, context, message);
#endif
    }

    #endregion

    #region Warning Logging (For Recoverable or Non-Critical Issues)

    /// <summary>
    /// Logs a warning. Use for unexpected states or configurations that the system can
    /// recover from but may indicate a designer oversight or a potential future problem.
    /// Warnings appear in Godot's debugger but do not halt execution.
    /// Prefer using string interpolation for dynamic values: JmoLogger.Warning(this, $"Missing optional: {name}")
    /// </summary>
    /// <param name="context">The object (Node, Resource, etc.) that is the source of the warning.</param>
    /// <param name="message">The warning message. Use string interpolation for dynamic values.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object.</param>
    /// <param name="callerFilePath">Auto-populated. Do not pass manually.</param>
    /// <param name="callerLineNumber">Auto-populated. Do not pass manually.</param>
    /// <param name="callerMemberName">Auto-populated. Do not pass manually.</param>
    public static void Warning(
        object context,
        string message,
        Node? owner = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        if (!IsEnabled(LogLevel.Warning))
        {
            return;
        }
        GD.PushWarning(BuildLogMessage("WARNING", context, message, owner, callerFilePath, callerLineNumber, callerMemberName));
#if TOOLS
        EmitTestSignal(LogLevel.Warning, context, message);
#endif
    }

    #endregion

    #region Info Logging (For General Diagnostic/Trace Messages)

    /// <summary>
    /// Logs an informational message. Use for tracing application flow, state changes,
    /// or other diagnostics that are useful during development but not indicative of a problem.
    /// Info messages appear as standard console output in Godot.
    /// Prefer using string interpolation for dynamic values: JmoLogger.Info(this, $"State changed to: {newState}")
    /// </summary>
    /// <param name="context">The object (Node, Resource, etc.) that is the source of the message.</param>
    /// <param name="message">The info message. Use string interpolation for dynamic values.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object.</param>
    /// <param name="callerFilePath">Auto-populated. Do not pass manually.</param>
    /// <param name="callerLineNumber">Auto-populated. Do not pass manually.</param>
    /// <param name="callerMemberName">Auto-populated. Do not pass manually.</param>
    public static void Info(
        object context,
        string message,
        Node? owner = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        if (!IsEnabled(LogLevel.Info))
        {
            return;
        }
        GD.Print(BuildLogMessage("INFO", context, message, owner, callerFilePath, callerLineNumber, callerMemberName));
#if TOOLS
        EmitTestSignal(LogLevel.Info, context, message);
#endif
    }

    #endregion

    #region Debug Logging (For High-Frequency Development Diagnostics)

    /// <summary>
    /// Logs a debug message for development diagnostics. Disabled by default — emitted only when
    /// <see cref="MinimumLevel"/> reaches <see cref="LogLevel.Debug"/> (equivalently,
    /// <see cref="DebugEnabled"/> is true). Use for high-frequency or verbose diagnostics that
    /// would clutter normal output.
    /// The early-return pattern avoids message-formatting cost when disabled.
    /// </summary>
    /// <param name="context">The object (Node, Resource, etc.) that is the source of the message.</param>
    /// <param name="message">The debug message. Use string interpolation for dynamic values.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object.</param>
    /// <param name="callerFilePath">Auto-populated. Do not pass manually.</param>
    /// <param name="callerLineNumber">Auto-populated. Do not pass manually.</param>
    /// <param name="callerMemberName">Auto-populated. Do not pass manually.</param>
    public static void Debug(
        object context,
        string message,
        Node? owner = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        if (!IsEnabled(LogLevel.Debug))
        {
            return;
        }
        GD.Print(BuildLogMessage("DEBUG", context, message, owner, callerFilePath, callerLineNumber, callerMemberName));
#if TOOLS
        EmitTestSignal(LogLevel.Debug, context, message);
#endif
    }

    #endregion

    #region Exception Handling

    /// <summary>
    /// Logs a caught exception and returns it, allowing the caller to re-throw it.
    /// This is the standard pattern for logging and propagating exceptions, as it makes
    /// the control flow clear to the C# compiler and ensures exceptions are not silently swallowed.
    /// Usage: throw JmoLogger.LogAndRethrow(ex, this);
    /// </summary>
    /// <param name="ex">The caught exception.</param>
    /// <param name="context">The object where the exception was caught.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object.</param>
    /// <param name="callerFilePath">Auto-populated. Do not pass manually.</param>
    /// <param name="callerLineNumber">Auto-populated. Do not pass manually.</param>
    /// <param name="callerMemberName">Auto-populated. Do not pass manually.</param>
    /// <returns>The original exception, to be thrown by the caller.</returns>
    public static Exception LogAndRethrow(
        Exception ex,
        object context,
        Node? owner = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        var message = $"Caught Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        GD.PushError(BuildLogMessage("EXCEPTION", context, message, owner, callerFilePath, callerLineNumber, callerMemberName));
        return ex;
    }

    /// <summary>
    /// Logs a caught exception that has been handled and will NOT be re-thrown.
    /// Use this in a catch block where you can gracefully recover from the error
    /// and continue execution without propagating the exception up the call stack.
    /// The output is a warning (not an error) because the program is continuing execution.
    /// </summary>
    /// <param name="ex">The caught exception.</param>
    /// <param name="context">The object where the exception was caught and handled.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object.</param>
    /// <param name="callerFilePath">Auto-populated. Do not pass manually.</param>
    /// <param name="callerLineNumber">Auto-populated. Do not pass manually.</param>
    /// <param name="callerMemberName">Auto-populated. Do not pass manually.</param>
    public static void LogHandledException(
        Exception ex,
        object context,
        Node? owner = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        var message = $"Handled Exception: {ex.GetType().Name}: {ex.Message}";
        GD.PushWarning(BuildLogMessage("HANDLED EXCEPTION", context, message, owner, callerFilePath, callerLineNumber, callerMemberName));
    }

    /// <summary>
    /// Logs an unhandled exception caught by the global FirstChanceException handler.
    /// Writes directly to godot.log via System.IO because GD.PushError is unreliable
    /// inside FirstChanceException handlers (Godot's bridge may be mid-exception-handling
    /// and silently drops re-entrant error calls).
    /// </summary>
    /// <param name="ex">The unhandled exception.</param>
    public static void LogUnhandledException(Exception ex)
    {
        var message = $"ERROR: [UNHANDLED EXCEPTION] {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}";
        try
        {
            var logPath = System.IO.Path.Combine(
                OS.GetUserDataDir(), "logs", "godot.log");
            System.IO.File.AppendAllText(logPath, message + "\n");
        }
        catch
        {
            // Last resort: write to stderr which Godot may capture
            System.Console.Error.WriteLine(message);
        }
    }

    #endregion
}
