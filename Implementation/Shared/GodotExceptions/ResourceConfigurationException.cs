namespace Jmodot.Implementation.Shared.GodotExceptions;

using System;
using Core.Shared.GodotExceptions;
using Godot;

/// <summary>
/// Represents an error that occurs when a Resource is not configured correctly
/// in the Godot editor (e.g., a required [Export] variable is not set).
/// </summary>
public class ResourceConfigurationException : GodotConfigurationException
{
    /// <summary>The name of the Resource where the configuration error occurred.</summary>
    public string ResourceName => ObjectName;

    public ResourceConfigurationException(string message, Resource resource)
        : base(message, resource.ResourceName) // <-- Pass the correct name property
    {
    }

    public ResourceConfigurationException(string message, Resource resource, Exception inner)
        : base(message, resource.ResourceName, inner)
    {
    }
}
