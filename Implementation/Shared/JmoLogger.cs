namespace Jmodot.Implementation.Shared;

using System;

/// <summary>
///     A centralized static class for logging with rich context. It standardizes message formats
///     across the project and leverages Godot's debugger for clear presentation of warnings and errors.
///     This is the definitive tool for all diagnostic output.
/// </summary>
public static class JmoLogger
{
    /// <summary>
    ///     The core private helper that builds the standardized log message string.
    /// </summary>
    private static string BuildLogMessage(string level, object context, string message, Node? owner = null,
        params object[] args)
    {
        string contextStr;
        if (context is Node node)
        {
            var ownerStr = owner != null ? $" (Owner: {owner.GetPath()})" :
                node.GetOwner() != null ? $" (Owner: {node.GetOwner().GetPath()})" : "";
            contextStr = $"[{node.GetType().Name} @ '{node.GetPath()}']{ownerStr}";
        }
        else if (context is Resource resource)
        {
            var ownerStr = owner != null ? $" (Owner: {owner.GetPath()})" : "";
            contextStr = $"[{resource.GetType().Name} @ '{resource.ResourcePath}']{ownerStr}";
        }
        else
        {
            contextStr = "[UNKNOWN CONTEXT]";
        }

        var finalMessage = args != null && args.Length > 0 ? string.Format(message, args) : message;

        return $"{contextStr} {level}: {finalMessage}";
    }

    #region Error Logging (For Critical, Non-Functional Bugs)

    /// <summary>
    ///     Logs a critical error originating from a Node. Use for setup or configuration errors
    ///     that prevent the Node from functioning as intended.
    /// </summary>
    /// <param name="context">The Node that is the source of the error.</param>
    /// <param name="owner">Optional. The Node that owns or is using this Resource, for crucial context.</param>
    /// <param name="message">The error message, which can contain format specifiers (e.g., "{0}").</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    public static void Error(Node context, string message, Node? owner = null, params object[] args)
    {
        var formattedMessage =
            BuildLogMessage("ERROR", context, message, owner ?? context.GetOwnerOrNull<Node>(), args);
        GD.PushError(formattedMessage);
    }

    /// <summary>
    ///     Logs a critical error originating from a Resource. Use for configuration errors
    ///     that make the Resource invalid or unusable.
    /// </summary>
    /// <param name="context">The Resource that is the source of the error.</param>
    /// <param name="owner">Optional. The Node that owns or is using this Resource, for crucial context.</param>
    /// <param name="message">The error message, which can contain format specifiers.</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    public static void Error(Resource context, string message, Node? owner = null, params object[] args)
    {
        var formattedMessage = BuildLogMessage("ERROR", context, message, owner, args);
        GD.PushError(formattedMessage);
    }

    #endregion

    #region Warning Logging (For Recoverable or Non-Critical Issues)

    /// <summary>
    ///     Logs a warning originating from a Node. Use for unexpected states or configurations
    ///     that the system can recover from but may indicate a designer oversight.
    /// </summary>
    /// <param name="context">The Node that is the source of the warning.</param>
    /// <param name="message">The warning message, which can contain format specifiers.</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    public static void Warning(Node context, string message, Node? custOwner = null, params object[] args)
    {
        var formattedMessage = BuildLogMessage("WARNING", context, message, custOwner, args);
        GD.PushWarning(formattedMessage);
    }

    /// <summary>
    ///     Logs a warning originating from a Resource.
    /// </summary>
    /// <param name="context">The Resource that is the source of the warning.</param>
    /// <param name="owner">Optional. The Node that owns or is using this Resource.</param>
    /// <param name="message">The warning message, which can contain format specifiers.</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    public static void Warning(Resource context, string message, Node? owner = null, params object[] args)
    {
        var formattedMessage = BuildLogMessage("WARNING", context, message, owner, args);
        GD.PushWarning(formattedMessage);
    }

    #endregion

    #region Info Logging (For General Diagnostic/Trace Messages)

    /// <summary>
    ///     Logs an informational message from a Node. Use for tracing application flow, state changes,
    ///     or other diagnostics that are useful during development but not indicative of a problem.
    /// </summary>
    /// <param name="context">The Node that is the source of the message.</param>
    /// <param name="message">The info message, which can contain format specifiers.</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    public static void Info(Node context, string message, params object[] args)
    {
        var formattedMessage = BuildLogMessage("INFO", context, message, null, args);
        GD.Print(formattedMessage);
    }

    /// <summary>
    ///     Logs an informational message from a Resource.
    /// </summary>
    /// <param name="context">The Resource that is the source of the message.</param>
    /// <param name="owner">Optional. The Node that owns or is using this Resource.</param>
    /// <param name="message">The info message, which can contain format specifiers.</param>
    /// <param name="args">Optional arguments to format into the message string.</param>
    public static void Info(Resource context, Node owner, string message, params object[] args)
    {
        var formattedMessage = BuildLogMessage("INFO", context, message, owner, args);
        GD.Print(formattedMessage);
    }

    #endregion

    #region Exception Handling

    /// <summary>
    ///     Logs a caught exception with full context and then re-throws it. Use in a catch block
    ///     when you want to add context to an exception before it propagates up the stack.
    /// </summary>
    public static void Exception(Exception ex, Node context, Node? custOwner = null)
    {
        var message = $"Caught Exception: {ex.Message}\n{ex.StackTrace}";
        var formattedMessage = BuildLogMessage("EXCEPTION", context, message, custOwner);
        GD.PushError(formattedMessage);
        throw ex;
    }

    /// <summary>
    ///     Logs a caught exception with full context and then re-throws it. Use in a catch block
    ///     when you want to add context to an exception before it propagates up the stack.
    /// </summary>
    public static void Exception(Exception ex, Resource context, Node? owner = null)
    {
        var message = $"Caught Exception: {ex.Message}\n{ex.StackTrace}";
        var formattedMessage = BuildLogMessage("EXCEPTION", context, message, owner);
        GD.PushError(formattedMessage);
        throw ex;
    }

    #endregion
}
