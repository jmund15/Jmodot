namespace Jmodot.Implementation.Shared.GodotExceptions;

using System;
using Core.Shared.GodotExceptions;
using Godot;

/// <summary>
/// Represents an error that occurs when a Node is not configured correctly
/// in the Godot editor (e.g., a required [Export] variable is not set).
/// </summary>
public class NodeConfigurationException : GodotConfigurationException
{
    /// <summary>The name of the node where the configuration error occurred.</summary>
    public string NodeName => ObjectName;

    public NodeConfigurationException(string message, Node node)
        : base(message, node.Name) // <-- Pass the name to the base constructor
    {
    }

    public NodeConfigurationException(string message, Node node, Exception inner)
        : base(message, node.Name, inner) // <-- Pass to the other base constructor
    {
    }
}
