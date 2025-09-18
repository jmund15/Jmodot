namespace Jmodot.Implementation.Shared;
using Godot;
using System;

/// <summary>
/// A centralized static class for logging with rich context. It standardizes message formats
/// across the project and leverages Godot's debugger for clear presentation of warnings and errors.
/// This is the definitive tool for all diagnostic output.
/// </summary>
public static class JmoLogger
{
    /// <summary>
    /// The core private helper that builds the standardized log message string based on the context object's type.
    /// </summary>
    /// <param name="level">The severity level of the log (e.g., "ERROR", "INFO").</param>
    /// <param name="context">The object that is the source of the log message.</param>
    /// <param name="message">The log message, which can contain format specifiers (e.g., "{0}").</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object, for additional clarity.</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    /// <returns>A fully formatted string ready for output.</returns>
    private static string BuildLogMessage(string level, object? context, string message, Node? owner, params object[] args)
    {
        // Gracefully handle cases where a null context is passed.
        if (context == null)
        {
            return $"[NULL CONTEXT] {level}: {message}";
        }

        string contextStr;
        string ownerStr = "";

        // Intelligently format the context string based on the object's type.
        switch (context)
        {
            case Node node:
                var nodeOwner = owner ?? node.GetOwner();
                ownerStr = nodeOwner != null ? $" (Owner: {nodeOwner.GetPath()})" : "";
                contextStr = $"[{node.GetType().Name} @ '{node.GetPath()}']{ownerStr}";
                break;
            case Resource resource:
                ownerStr = owner != null ? $" (Owner: {owner.GetPath()})" : "";
                contextStr = $"[{resource.GetType().Name} @ '{resource.ResourcePath}']{ownerStr}";
                break;
            default:
                // Provide a sensible fallback for any other C# object.
                contextStr = $"[{context.GetType().Name}]";
                break;
        }

        var finalMessage = args is { Length: > 0 } ? string.Format(message, args) : message;
        return $"{contextStr} {level}: {finalMessage}";
    }

    #region Error Logging (For Critical, Non-Functional Bugs)

    /// <summary>
    /// Logs a critical error. Use for setup, configuration, or runtime errors that
    /// prevent an object from functioning as intended.
    /// </summary>
    /// <param name="context">The object (Node, Resource, etc.) that is the source of the error.</param>
    /// <param name="message">The error message, which can contain format specifiers (e.g., "{0}").</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object, for crucial context.</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    public static void Error(object context, string message, Node? owner = null, params object[] args)
    {
        GD.PushError(BuildLogMessage("ERROR", context, message, owner, args));
    }

    #endregion

    #region Warning Logging (For Recoverable or Non-Critical Issues)

    /// <summary>
    /// Logs a warning. Use for unexpected states or configurations that the system can
    /// recover from but may indicate a designer oversight or a potential future problem.
    /// </summary>
    /// <param name="context">The object (Node, Resource, etc.) that is the source of the warning.</param>
    /// <param name="message">The warning message, which can contain format specifiers.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object.</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    public static void Warning(object context, string message, Node? owner = null, params object[] args)
    {
        GD.PushWarning(BuildLogMessage("WARNING", context, message, owner, args));
    }

    #endregion

    #region Info Logging (For General Diagnostic/Trace Messages)

    /// <summary>
    /// Logs an informational message. Use for tracing application flow, state changes,
    /// or other diagnostics that are useful during development but not indicative of a problem.
    /// </summary>
    /// <param name="context">The object (Node, Resource, etc.) that is the source of the message.</param>
    /// <param name="message">The info message, which can contain format specifiers.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object.</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    public static void Info(object context, string message, Node? owner = null, params object[] args)
    {
        GD.Print(BuildLogMessage("INFO", context, message, owner, args));
    }

    #endregion

    #region Exception Handling

    /// <summary>
    /// Logs a caught exception and returns it, allowing the caller to re-throw it.
    /// This is the standard pattern for logging and propagating exceptions, as it makes
    /// the control flow clear to the C# compiler.
    /// Usage: throw JmoLogger.LogAndRethrow(ex, this);
    /// </summary>
    /// <param name="ex">The caught exception.</param>
    /// <param name="context">The object where the exception was caught.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object.</param>
    /// <returns>The original exception, to be thrown by the caller.</returns>
    public static Exception LogAndRethrow(Exception ex, object context, Node? owner = null)
    {
        var message = $"Caught Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
        GD.PushError(BuildLogMessage("EXCEPTION", context, message, owner));
        return ex;
    }

    /// <summary>
    /// Logs a caught exception that has been handled and will NOT be re-thrown.
    /// Use this in a catch block where you can gracefully recover from the error.
    /// The output is a warning because the program is continuing execution.
    /// </summary>
    /// <param name="ex">The caught exception.</param>
    /// <param name="context">The object where the exception was caught and handled.</param>
    /// <param name="owner">Optional. The Node that owns or is using the context object.</param>
    public static void LogHandledException(Exception ex, object context, Node? owner = null)
    {
        var message = $"Handled Exception: {ex.GetType().Name}: {ex.Message}";
        GD.PushWarning(BuildLogMessage("HANDLED EXCEPTION", context, message, owner));
    }

    #endregion
}
