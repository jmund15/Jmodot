namespace Jmodot.Implementation.Shared;
using Godot;
using System;
using System.Runtime.CompilerServices;

/// <summary>
/// A centralized static class for logging with rich context. It standardizes message formats
/// across the project and leverages Godot's debugger for clear presentation of warnings and errors.
/// This is the definitive tool for all diagnostic output.
/// </summary>
public static class JmoLogger
{
    private const string DEBUG_ENABLED_SETTING = "debug/jmodot/debug_logging_enabled";

    // Cache to avoid ProjectSettings lookup on every Debug() call
    private static bool? _debugEnabledCache;

    /// <summary>
    /// Controls whether Debug() messages are output. Disabled by default.
    /// Enable during development to see verbose diagnostic information.
    /// Can be configured via Project Settings → Debug → Jmodot → Debug Logging Enabled.
    /// </summary>
    public static bool DebugEnabled
    {
        get => _debugEnabledCache ??= (bool)ProjectSettings.GetSetting(DEBUG_ENABLED_SETTING, false);
        set
        {
            _debugEnabledCache = value;
            ProjectSettings.SetSetting(DEBUG_ENABLED_SETTING, value);
        }
    }

    /// <summary>
    /// Registers the debug setting with ProjectSettings so it appears in the editor UI.
    /// Call this once during project initialization (e.g., from Global autoload).
    /// </summary>
    public static void RegisterProjectSettings()
    {
        // Set default value if not already present
        if (!ProjectSettings.HasSetting(DEBUG_ENABLED_SETTING))
        {
            ProjectSettings.SetSetting(DEBUG_ENABLED_SETTING, false);
        }

        // Register property info so it appears with proper UI (checkbox)
        var propertyInfo = new Godot.Collections.Dictionary
        {
            { "name", DEBUG_ENABLED_SETTING },
            { "type", (int)Variant.Type.Bool }
        };
        ProjectSettings.AddPropertyInfo(propertyInfo);
        ProjectSettings.SetAsBasic(DEBUG_ENABLED_SETTING, true);

        // Refresh cache from saved setting
        _debugEnabledCache = (bool)ProjectSettings.GetSetting(DEBUG_ENABLED_SETTING, false);
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
                var pathStr = (node.IsInsideTree()) ? node.GetPath().ToString() : "[Detached]";
                contextStr = $"[{node.GetType().Name} @ '{pathStr}']{ownerStr}";
                break;
            case Resource resource:
                ownerStr = owner != null ? $" (Owner: {owner.GetPath()})" : "";
                contextStr = $"[{resource.GetType().Name} @ '{resource.ResourcePath}']{ownerStr}";
                break;
            default:
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
        GD.PushWarning(BuildLogMessage("WARNING", context, message, owner, callerFilePath, callerLineNumber, callerMemberName));
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
        GD.Print(BuildLogMessage("INFO", context, message, owner, callerFilePath, callerLineNumber, callerMemberName));
    }

    #endregion

    #region Debug Logging (For High-Frequency Development Diagnostics)

    /// <summary>
    /// Logs a debug message for development diagnostics. These messages are disabled by default
    /// and only appear when DebugEnabled is set to true. Use for high-frequency or verbose
    /// diagnostics that would clutter normal output.
    /// The early-return pattern avoids string building cost when disabled.
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
        if (!DebugEnabled)
        {
            return;
        }
        GD.Print(BuildLogMessage("DEBUG", context, message, owner, callerFilePath, callerLineNumber, callerMemberName));
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

    #endregion
}
