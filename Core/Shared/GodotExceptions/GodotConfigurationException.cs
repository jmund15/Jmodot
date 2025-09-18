namespace Jmodot.Core.Shared.GodotExceptions;

using System;
using Godot;

/// <summary>
/// The base class for exceptions that occur when a Godot object (like a Node or Resource)
/// is not configured correctly in the editor. This class should not be instantiated directly.
/// </summary>
public abstract class GodotConfigurationException : Exception
{
    /// <summary>
    /// Gets the name of the Godot object that was misconfigured.
    /// </summary>
    public string ObjectName { get; }

    protected GodotConfigurationException(string message, string objectName)
        : base($"[{objectName}] {message}")
    {
        ObjectName = objectName;
    }

    protected GodotConfigurationException(string message, string objectName, Exception inner)
        : base($"[{objectName}] {message}", inner)
    {
        ObjectName = objectName;
    }
}
