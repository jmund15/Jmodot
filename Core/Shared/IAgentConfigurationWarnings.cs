namespace Jmodot.Core.Shared;

using Godot;

/// <summary>
/// A <see cref="Resource"/> whose validity depends on the entity it is mounted on, such as a target
/// that needs a perception manager beside the agent. A host that lists the Resource queries this
/// capability, so no host tests a Resource type and a new target-bearing Resource costs no host edit.
/// </summary>
public interface IAgentConfigurationWarnings
{
    /// <summary>
    /// One entry per fault the entity <paramref name="agent"/> causes, worded without the slot path
    /// the host adds. Empty when the entity satisfies the Resource.
    /// </summary>
    string[] GetAgentWarnings(Node agent);
}
